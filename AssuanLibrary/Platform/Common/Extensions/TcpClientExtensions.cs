// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace AssuanLibrary.Platform.Common.Extensions;

/// <summary>
///   Extension methods for <see cref="TcpClient" />.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class TcpClientExtensions {
  extension(TcpClient tcpClient) {
    /// <summary>
    ///   Discards any available data in the TCP client receive buffer.
    /// </summary>
    public void DiscardAvailableData() {
      if (tcpClient.Available == 0) {
        return;
      }

      var networkStream = tcpClient.GetStream();
      var buffer = ArrayPool<byte>.Shared.Rent(tcpClient.Available);
      _ = networkStream.Read(buffer, 0, tcpClient.Available);
      ArrayPool<byte>.Shared.Return(buffer, true);
    }

    /// <summary>
    ///   Discards any available data in the TCP client receive buffer asynchronously.
    /// </summary>
    /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    public async Task DiscardAvailableDataAsync(CancellationToken ct = default) {
      if (tcpClient.Available == 0) {
        return;
      }

      var networkStream = tcpClient.GetStream();
      var buffer = ArrayPool<byte>.Shared.Rent(tcpClient.Available);
      _ = await networkStream.ReadAsync(buffer, 0, tcpClient.Available, ct);
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }
}
