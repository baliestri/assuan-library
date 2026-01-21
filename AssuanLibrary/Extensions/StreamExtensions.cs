// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Extensions;

/// <summary>
///   Extension methods for <see cref="Stream" />.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class StreamExtensions {
  extension(Stream stream) {
    /// <summary>
    ///   Writes the contents of the specified <see cref="ReadOnlyMemory{Byte}" /> to the stream.
    /// </summary>
    /// <param name="buffer">The buffer containing the data to write.</param>
    public void Write(byte[] buffer)
      => stream.Write(buffer, 0, buffer.Length);
  }
}
