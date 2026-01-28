// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using AssuanLibrary.Platform.Unix.Polyfills;

namespace AssuanLibrary.Platform.Unix.Extensions;

/// <summary>
///   Extension methods for <see cref="Socket" />.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class SocketExtensions {
  extension(Socket socket) {
    /// <summary>
    ///   Discards any available data in the TCP client receive buffer.
    /// </summary>
    public void DiscardAvailableData() {
      if (socket.Available == 0) {
        return;
      }

      var buffer = ArrayPool<byte>.Shared.Rent(socket.Available);
      _ = socket.Receive(buffer, socket.Available, SocketFlags.None);
      ArrayPool<byte>.Shared.Return(buffer, true);
    }

    /// <summary>
    ///   Discards any available data in the TCP client receive buffer asynchronously.
    /// </summary>
    public async Task DiscardAvailableDataAsync(CancellationToken ct = default) {
      if (socket.Available == 0) {
        return;
      }

      var buffer = ArrayPool<byte>.Shared.Rent(socket.Available);
      var segment = new ArraySegment<byte>(buffer, 0, socket.Available);
      _ = await socket.ReceiveAsync(segment, SocketFlags.None, ct).ConfigureAwait(false);
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }
}
