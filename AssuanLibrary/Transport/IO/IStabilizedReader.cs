// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.IO;

/// <summary>
///   A reader that reads data from a stream until it stabilizes.
/// </summary>
public interface IStabilizedReader : IDisposable {
  /// <summary>
  ///   Reads data from the stream until it stabilizes or the timeout is reached.
  /// </summary>
  /// <returns>The read data as a byte array.</returns>
  /// <exception cref="TimeoutException">Thrown if the read operation times out.</exception>
  byte[] Read();

  /// <summary>
  ///   Asynchronously reads data from the stream until it stabilizes or the timeout is reached.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous read operation. The value of the TResult parameter contains the read data as a byte array.</returns>
  /// <exception cref="TimeoutException">Thrown if the read operation times out.</exception>
  ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default);
}
