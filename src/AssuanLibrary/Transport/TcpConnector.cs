// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;

namespace AssuanLibrary.Transport;

/// <summary>Creates an explicit TCP byte stream without greeting or authentication.</summary>
public static class TcpConnector {
  /// <summary>Connects within a single total deadline, ten seconds by default.</summary>
  /// <remarks>TCP, including loopback, supplies no OS-authenticated user identity.</remarks>
  public static async Task<Stream> ConnectAsync(IPEndPoint endpoint, TimeSpan? timeout = null,
  CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(endpoint);
    var deadline = new TransportDeadline(timeout ?? TimeSpan.FromSeconds(10));
    if (endpoint.Port == 0 ||
        IPAddress.Any.Equals(endpoint.Address) ||
        IPAddress.IPv6Any.Equals(endpoint.Address)) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    if (cancellationToken.IsCancellationRequested) {
      throw new TransportException(TransportErrorKind.Cancelled);
    }

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var remaining = deadline.Remaining;
    if (remaining == TimeSpan.Zero) {
      throw new TransportException(TransportErrorKind.Timeout);
    }

    linked.CancelAfter(remaining);
    var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    var transferred = false;
    try {
      await socket.ConnectAsync(endpoint, linked.Token).ConfigureAwait(false);
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      var stream = new NetworkStream(socket, true);
      transferred = true;
      return stream;
    }
    catch (OperationCanceledException) {
      throw new TransportException(cancellationToken.IsCancellationRequested
        ? TransportErrorKind.Cancelled
        : TransportErrorKind.Timeout);
    }
    catch (SocketException error) when (linked.IsCancellationRequested) {
      throw new TransportException(cancellationToken.IsCancellationRequested
        ? TransportErrorKind.Cancelled
        : TransportErrorKind.Timeout, error);
    }
    catch (SocketException error) {
      throw new TransportException(TransportErrorKind.Io, error);
    }
    catch (IOException error) {
      throw new TransportException(TransportErrorKind.Io, error);
    }
    finally {
      if (!transferred) {
        socket.Dispose();
      }
    }
  }
}
