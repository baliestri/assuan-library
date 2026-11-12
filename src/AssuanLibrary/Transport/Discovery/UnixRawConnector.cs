// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using AssuanLibrary.Transport.Unix;

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Connects using original Unix pathname bytes without replacement-decoding them.</summary>
internal static class UnixRawConnector {
  [SuppressMessage("Design", "MA0051:Method is too long",
    Justification = "The nonblocking socket is owned until it is duplicated into the returned stream; one finally " +
                    "must cover the connect poll, the duplication and the final deadline check.")]
  internal static async Task<Stream> ConnectAsync(ReadOnlyMemory<byte> path, TimeSpan timeout,
    CancellationToken cancellationToken) {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      throw new PlatformNotSupportedException();
    }

    var deadline = new TransportDeadline(timeout);
    var maxPath = OperatingSystem.IsLinux() ? 107 : 103;
    if (path.IsEmpty ||
        path.Length > maxPath ||
        path.Span[0] != (byte)'/' ||
        path.Span.IndexOf((byte)0) >= 0) {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    if (cancellationToken.IsCancellationRequested) {
      throw new TransportException(TransportErrorKind.Cancelled);
    }

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    linked.CancelAfter(deadline.Remaining);
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    var transferred = false;
    try {
      socket.Blocking = false;
      if (!UnixNative.ConnectRaw(socket, path.Span)) {
        while (true) {
          if (deadline.Remaining == TimeSpan.Zero) {
            throw new TransportException(TransportErrorKind.Timeout);
          }

          linked.Token.ThrowIfCancellationRequested();
          if (socket.Poll(0, SelectMode.SelectWrite)) {
            if (socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error) is not int error) {
              throw new TransportException(TransportErrorKind.AccessPolicy);
            }

            if (error != 0) {
              throw new TransportException(TransportErrorKind.Io, new SocketException(error));
            }

            break;
          }

          await Task.Delay(TimeSpan.FromMilliseconds(10), linked.Token).ConfigureAwait(false);
        }
      }

      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      var connectedSocket = UnixNative.DuplicateConnectedSocket(socket);
      NetworkStream result;
      try { result = new NetworkStream(connectedSocket, true); }
      catch {
        connectedSocket.Dispose();
        throw;
      }

      socket.Dispose();
      if (deadline.Remaining == TimeSpan.Zero ||
          cancellationToken.IsCancellationRequested) {
        await result.DisposeAsync().ConfigureAwait(false);
        throw new TransportException(cancellationToken.IsCancellationRequested
          ? TransportErrorKind.Cancelled
          : TransportErrorKind.Timeout);
      }

      transferred = true;
      return result;
    }
    catch (OperationCanceledException) {
      throw new TransportException(cancellationToken.IsCancellationRequested
        ? TransportErrorKind.Cancelled
        : TransportErrorKind.Timeout);
    }
    catch (SocketException error) {
      throw new TransportException(TransportErrorKind.Io, error);
    }
    finally {
      if (!transferred) {
        socket.Dispose();
      }
    }
  }
}
