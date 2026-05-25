// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;

namespace AssuanLibrary.Platform.Windows.Extensions;

[ExcludeFromCodeCoverage]
internal static class NamedPipeStreamExtensions {
  /// <summary>
  ///   Extension methods for <see cref="PipeStream" />.
  /// </summary>
  extension(PipeStream pipeStream) {
    /// <summary>
    ///   Discards any available data in the named pipe stream receive buffer.
    /// </summary>
    public void DiscardAvailableData() {
      var buffer = ArrayPool<byte>.Shared.Rent(4096);
      try {
        while (pipeStream is { CanRead: true, InBufferSize: > 0 }) {
          var read = pipeStream.Read(buffer, 0, Math.Min(buffer.Length, pipeStream.InBufferSize));
          if (read == 0) {
            break;
          }

          if (pipeStream.IsMessageComplete) {
            break;
          }
        }
      }
      finally {
        ArrayPool<byte>.Shared.Return(buffer, true);
      }
    }

    /// <summary>
    ///   Discards any available data in the named pipe stream receive buffer asynchronously.
    /// </summary>
    /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    public async Task DiscardAvailableDataAsync(CancellationToken ct = default) {
      var buffer = ArrayPool<byte>.Shared.Rent(4096);
      try {
        while (pipeStream is { CanRead: true, InBufferSize: > 0 }) {
          var read = await pipeStream.ReadAsync(buffer, 0, Math.Min(buffer.Length, pipeStream.InBufferSize), ct)
            .ConfigureAwait(false);
          if (read == 0) {
            break;
          }

          if (pipeStream.IsMessageComplete) {
            break;
          }
        }
      }
      finally {
        ArrayPool<byte>.Shared.Return(buffer, true);
      }
    }
  }
}
