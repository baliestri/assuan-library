// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;

namespace AssuanLibrary.Transport;

/// <summary>Explicit TCP listener, with no authenticated OS peer identity.</summary>
public sealed class TcpAcceptor : IAssuanAcceptor {
  private readonly Socket _listener;
  private int _disposed;

  private TcpAcceptor(Socket listener) {
    _listener = listener;
    Endpoint = (IPEndPoint)listener.LocalEndPoint!;
  }

  /// <summary>The actual bound address, including an OS-assigned port for bind port zero.</summary>
  public IPEndPoint Endpoint { get; }

  /// <summary>Accepts one connection; cancellation does not dispose the listener.</summary>
  public async ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
    if (Volatile.Read(ref _disposed) != 0) {
      throw new TransportException(TransportErrorKind.Closed);
    }

    try {
      var socket = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
      return new AcceptedConnection(new NetworkStream(socket, true));
    }
    catch (OperationCanceledException) {
      throw new TransportException(TransportErrorKind.Cancelled);
    }
    catch (ObjectDisposedException) {
      throw new TransportException(TransportErrorKind.Closed);
    }
    catch (SocketException error) {
      throw new TransportException(Volatile.Read(ref _disposed) != 0 ? TransportErrorKind.Closed : TransportErrorKind.Io,
        error);
    }
  }

  /// <summary>Closes the listener without closing already accepted streams.</summary>
  public ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0) {
      _listener.Dispose();
    }

    return ValueTask.CompletedTask;
  }

  /// <summary>Binds the selected address. Bind loopback explicitly if remote exposure is not needed.</summary>
  public static TcpAcceptor Bind(IPEndPoint endpoint) {
    ArgumentNullException.ThrowIfNull(endpoint);
    var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    try {
      socket.Bind(endpoint);
      socket.Listen();
      return new TcpAcceptor(socket);
    }
    catch (SocketException error) {
      socket.Dispose();
      throw new TransportException(TransportErrorKind.Io, error);
    }
    catch {
      socket.Dispose();
      throw;
    }
  }
}
