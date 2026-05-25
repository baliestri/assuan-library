// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AssuanLibrary.Polyfills;

/// <summary>
///   Polyfill extension methods for <see cref="Stream" />.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class StreamPolyfillExtensions {
  extension(Stream stream) {
    /// <summary>
    ///   Writes the contents of the specified <see cref="ReadOnlyMemory{Byte}" /> to the stream.
    /// </summary>
    /// <param name="buffer">The buffer containing the data to write.</param>
    public void Write(ReadOnlyMemory<byte> buffer) {
      if (!MemoryMarshal.TryGetArray(buffer, out var arraySegment)) {
        arraySegment = new ArraySegment<byte>(buffer.ToArray());
      }

      stream.Write(arraySegment.Array!, arraySegment.Offset, arraySegment.Count);
    }
  }
}
