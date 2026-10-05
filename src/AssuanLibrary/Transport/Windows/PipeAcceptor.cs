// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;

namespace AssuanLibrary.Transport.Windows;

/// <summary>Current-user-only, local byte-mode Windows pipe listener.</summary>
public sealed class PipeAcceptor : IAssuanAcceptor {
  private readonly string _path;
  private readonly byte[] _securityDescriptor;
  private int _accepting;
  private int _disposed;
  private NamedPipeServerStream? _waiting;

  private PipeAcceptor(string path, byte[] descriptor, NamedPipeServerStream waiting) {
    _path = path;
    _securityDescriptor = descriptor;
    _waiting = waiting;
  }

  /// <summary>Accepts a stream; the listener maintains a separate next waiting instance.</summary>
  public async ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
    if (Volatile.Read(ref _disposed) != 0) {
      throw new TransportException(TransportErrorKind.Closed);
    }

    if (Interlocked.CompareExchange(ref _accepting, 1, 0) != 0) {
      throw new InvalidOperationException("Another accept is already pending.");
    }

    try {
      var waiting = _waiting ?? throw new TransportException(TransportErrorKind.Closed);
      await waiting.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
      if (Volatile.Read(ref _disposed) != 0) {
        throw new TransportException(TransportErrorKind.Closed);
      }

      try {
        _waiting = PipeNative.CreateWaiting(_path, _securityDescriptor, false);
      }
      catch {
        Interlocked.Exchange(ref _disposed, 1);
        _waiting = null;
        await waiting.DisposeAsync();
        throw;
      }

      return new AcceptedConnection(new PipeByteStream(waiting));
    }
    catch (OperationCanceledException) {
      if (_waiting?.IsConnected == true) {
        await _waiting.DisposeAsync();
        _waiting = PipeNative.CreateWaiting(_path, _securityDescriptor, false);
      }

      throw new TransportException(TransportErrorKind.Cancelled);
    }
    catch (ObjectDisposedException) {
      throw new TransportException(TransportErrorKind.Closed);
    }
    catch (IOException error) {
      throw new TransportException(TransportErrorKind.Io, error);
    }
    finally {
      Volatile.Write(ref _accepting, 0);
    }
  }

  /// <summary>Closes only the waiting listener instance, not streams already accepted.</summary>
  public ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0) {
      Interlocked.Exchange(ref _waiting, null)?.Dispose();
    }

    return ValueTask.CompletedTask;
  }

  /// <summary>Creates an exclusive first instance with protected current-user DACL.</summary>
  public static PipeAcceptor Bind(LocalEndpoint endpoint) {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    ArgumentNullException.ThrowIfNull(endpoint);

    if (endpoint.Kind != LocalEndpointKind.NamedPipe) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    var descriptor = PipeSecurityFactory.ForCurrentUser().GetSecurityDescriptorBinaryForm();
    var waiting = PipeNative.CreateWaiting(endpoint.Path, descriptor, true);
    return new PipeAcceptor(endpoint.Path, descriptor, waiting);
  }
}
