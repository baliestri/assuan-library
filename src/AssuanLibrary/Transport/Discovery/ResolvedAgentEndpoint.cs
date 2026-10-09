// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport.Unix;

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Kind of an existing agent's resolved local endpoint.</summary>
public enum AgentEndpointKind {
  /// <summary>An existing Unix-domain socket addressed by original path bytes.</summary>
  UnixSocket,

  /// <summary>A native Windows loopback socket requiring its private nonce.</summary>
  NativeWindowsAgent
}

/// <summary>An owned discovered address and optional private Windows handshake nonce.</summary>
/// <remarks>Use <see cref="ConnectAsync" /> to send the native Windows nonce before reading the Assuan greeting.</remarks>
public sealed class ResolvedAgentEndpoint : IDisposable {
  private readonly AgentEndpointKind _kind;
  private readonly SecretBuffer? _nonce;
  private readonly IPEndPoint? _tcpEndpoint;
  private readonly byte[]? _unixPath;
  private int _active;
  private int _disposed;
  private int _pendingNonceIo;

  private ResolvedAgentEndpoint(AgentEndpointKind kind, byte[]? unixPath, IPEndPoint? tcp, SecretBuffer? nonce) {
    _kind = kind;
    _unixPath = unixPath;
    _tcpEndpoint = tcp;
    _nonce = nonce;
  }

  /// <summary>The platform-specific endpoint category.</summary>
  public AgentEndpointKind Kind {
    get {
      CheckDisposed();
      return _kind;
    }
  }

  /// <summary>Explicit original Unix pathname bytes; no lossy text conversion is performed.</summary>
  public ReadOnlySpan<byte> UnixPathBytes {
    get {
      CheckDisposed();
      return _unixPath ?? throw new InvalidOperationException("The resolved endpoint is not a Unix socket.");
    }
  }

  /// <summary>The Windows loopback address without its nonce, or null on Unix.</summary>
  /// <remarks>Connecting to this address alone omits the native agent handshake.</remarks>
  public IPEndPoint? TcpEndpoint {
    get {
      CheckDisposed();
      return _tcpEndpoint is null ? null : new IPEndPoint(IPAddress.Loopback, _tcpEndpoint.Port);
    }
  }

  /// <summary>Prevents new connections and wipes the nonce once in-flight use is complete.</summary>
  public void Dispose() {
    Interlocked.Exchange(ref _disposed, 1);
    WipeIfDisposed();
  }

  internal static ResolvedAgentEndpoint ForUnix(byte[] path)
    => new(AgentEndpointKind.UnixSocket, path, null, null);

  internal static ResolvedAgentEndpoint ForWindows(ushort port, SecretBuffer nonce)
    => new(AgentEndpointKind.NativeWindowsAgent, null, new IPEndPoint(IPAddress.Loopback, port), nonce);

  /// <summary>Connects and sends a native Windows nonce, if any, within one total deadline.</summary>
  /// <remarks>The returned owned stream has not consumed the Assuan greeting.</remarks>
  public Task<Stream> ConnectAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    => ConnectWithAsync(TcpConnector.ConnectAsync, timeout, cancellationToken);

  // Injection is internal: deterministic tests can stall the nonce write without depending on TCP buffer sizes.
  internal async Task<Stream> ConnectWithAsync(Func<IPEndPoint, TimeSpan?, CancellationToken, Task<Stream>> tcpConnect,
  TimeSpan? timeout, CancellationToken cancellationToken) {
    CheckDisposed();
    if (Volatile.Read(ref _pendingNonceIo) != 0 ||
        Interlocked.CompareExchange(ref _active, 1, 0) != 0) {
      throw new InvalidOperationException("Another agent connection still owns the resolved endpoint.");
    }

    Stream? stream = null;
    var transferred = false;
    try {
      var deadline = new TransportDeadline(timeout ?? TimeSpan.FromSeconds(10));
      if (cancellationToken.IsCancellationRequested) {
        throw new TransportException(TransportErrorKind.Cancelled);
      }

      if (_kind == AgentEndpointKind.UnixSocket) {
        var path = _unixPath!;
        try {
          var text = new UTF8Encoding(false, true).GetString(path);
          stream = await UnixConnector.ConnectAsync(LocalEndpoint.Unix(text), deadline.Remaining, cancellationToken)
            .ConfigureAwait(false);
        }
        catch (DecoderFallbackException) {
          stream = await UnixRawConnector.ConnectAsync(path, deadline.Remaining, cancellationToken).ConfigureAwait(false);
        }
      }
      else {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(deadline.Remaining);
        stream = await tcpConnect(_tcpEndpoint!, deadline.Remaining, linked.Token).ConfigureAwait(false);
        var write = stream.WriteAsync(_nonce!.ExposeMemoryForIo(), linked.Token).AsTask();
        try { await write.WaitAsync(deadline.Remaining, linked.Token).ConfigureAwait(false); }
        catch {
          if (!write.IsCompleted) {
            DeferNonceCleanup(write);
          }

          throw;
        }

        var flush = stream.FlushAsync(linked.Token);
        try { await flush.WaitAsync(deadline.Remaining, linked.Token).ConfigureAwait(false); }
        catch {
          if (!flush.IsCompleted) {
            ObserveLater(flush);
          }

          throw;
        }
      }

      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      CheckDisposed();
      transferred = true;
      return stream;
    }
    catch (OperationCanceledException) {
      throw new TransportException(cancellationToken.IsCancellationRequested
        ? TransportErrorKind.Cancelled
        : TransportErrorKind.Timeout);
    }
    catch (TimeoutException) {
      throw new TransportException(TransportErrorKind.Timeout);
    }
    catch (IOException error) {
      throw new TransportException(TransportErrorKind.Io, error);
    }
    finally {
      if (!transferred) {
        stream?.Dispose();
      }

      Interlocked.Exchange(ref _active, 0);
      WipeIfDisposed();
    }
  }

  /// <inheritdoc />
  public override string ToString()
    => $"ResolvedAgentEndpoint {{ Kind = {_kind}, HasNonce = {_nonce is not null} }}";

  private void CheckDisposed()
    => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

  private void WipeIfDisposed() {
    if (Volatile.Read(ref _disposed) == 0 ||
        Volatile.Read(ref _active) != 0 ||
        Volatile.Read(ref _pendingNonceIo) != 0) {
      return;
    }

    _nonce?.Dispose();
    if (_unixPath is not null) {
      Array.Clear(_unixPath);
    }
  }

  private void DeferNonceCleanup(Task write) {
    Volatile.Write(ref _pendingNonceIo, 1);
    _ = write.ContinueWith(completed => {
      _ = completed.Exception;
      Volatile.Write(ref _pendingNonceIo, 0);
      WipeIfDisposed();
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
  }

  private static void ObserveLater(Task pending)
    => _ = pending.ContinueWith(completed => {
      _ = completed.Exception;
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
