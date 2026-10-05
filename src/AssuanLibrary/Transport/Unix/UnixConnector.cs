// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;

namespace AssuanLibrary.Transport.Unix;

/// <summary>Connects to an existing Unix byte socket without imposing listener bind policy on its parent.</summary>
public static class UnixConnector {
  /// <summary>Connects within a total deadline, ten seconds by default, without reading any greeting.</summary>
  public static async Task<Stream> ConnectAsync(LocalEndpoint endpoint, TimeSpan? timeout = null,
  CancellationToken cancellationToken = default) {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      throw new PlatformNotSupportedException();
    }

    ArgumentNullException.ThrowIfNull(endpoint);

    if (endpoint.Kind != LocalEndpointKind.UnixSocket) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    var deadline = new TransportDeadline(timeout ?? TimeSpan.FromSeconds(10));
    if (cancellationToken.IsCancellationRequested) {
      throw new TransportException(TransportErrorKind.Cancelled);
    }

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var remaining = deadline.Remaining;
    if (remaining == TimeSpan.Zero) {
      throw new TransportException(TransportErrorKind.Timeout);
    }

    linked.CancelAfter(remaining);
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    var transferred = false;
    try {
      await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint.Path), linked.Token).ConfigureAwait(false);
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      var result = new NetworkStream(socket, true);
      transferred = true;
      return result;
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
    catch (ArgumentException) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }
    finally {
      if (!transferred) {
        socket.Dispose();
      }
    }
  }
}
