// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Net.Sockets;

namespace AssuanLibrary.Network.Utility;

/// <summary>
///   A reader that reads from a TCP stream until the data stabilizes, indicating no more data is incoming.
/// </summary>
/// <param name="tcpClient">The TCP client to read from.</param>
/// <param name="timeout">The maximum duration to wait for the stream to stabilize before timing out.</param>
public struct StabilizedStreamReader(TcpClient tcpClient, TimeSpan timeout) : IDisposable {
  private const int INITIAL_BUFFER_CAPACITY = 4096;
  private const int DEFAULT_RENT_BUFFER_EXTRA = 32;
  private readonly MemoryStream _memoryStream = new(INITIAL_BUFFER_CAPACITY);
  private readonly NetworkStream _networkStream = tcpClient.GetStream();
  private int _written;
  private bool _disposed;

  /// <summary>
  ///   The duration to wait after the last received data chunk before considering the stream to be stable. Defaults to 150 milliseconds.
  /// </summary>
  public TimeSpan StabilizationDelay { get; set; } = TimeSpan.FromMilliseconds(150);

  /// <summary>
  ///   The grace period to wait after reading data before checking for more data. Defaults to 40 milliseconds.
  /// </summary>
  public TimeSpan ReadGracePeriod { get; set; } = TimeSpan.FromMilliseconds(40);

  /// <summary>
  ///   The interval between polls to check for available data. Defaults to 50 milliseconds.
  /// </summary>
  public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(50);

  /// <summary>
  ///   Reads data from the stream until it stabilizes or the timeout is reached.
  /// </summary>
  /// <returns>The read data as a byte array.</returns>
  /// <exception cref="TimeoutException">Thrown if the read operation times out.</exception>
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedStreamReader));

    var deadline = DateTime.UtcNow + timeout;
    var consecutiveZeros = 0;
    var zeroStartedAt = default(DateTime);

    while (DateTime.UtcNow <= deadline) {
      var available = tcpClient.Available;

      if (available > 0) {
        ReadChunk(available);
        _written += available;
        consecutiveZeros = 0;
        zeroStartedAt = default;
        Task.Delay(ReadGracePeriod);
        continue;
      }

      UpdateZeroState(available, ref consecutiveZeros, ref zeroStartedAt);

      if (IsStableIdle(consecutiveZeros, zeroStartedAt)) {
        break;
      }

      Task.Delay(PollInterval);
    }

    if (DateTime.UtcNow > deadline) {
      throw new TimeoutException("Reading from the stream timed out.");
    }

    var output = new byte[_written];

    _memoryStream.Seek(0, SeekOrigin.Begin);
    _ = _memoryStream.Read(output, 0, _written);
    _memoryStream.SetLength(0);
    _written = 0;

    return output;
  }

  /// <summary>
  ///   Asynchronously reads data from the stream until it stabilizes or the timeout is reached.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous read operation. The value of the TResult parameter contains the read data as a byte array.</returns>
  /// <exception cref="TimeoutException">Thrown if the read operation times out.</exception>
  public async ValueTask<byte[]> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedStreamReader));

    var deadline = DateTime.UtcNow + timeout;
    var consecutiveZeros = 0;
    var zeroStartedAt = default(DateTime);

    while (DateTime.UtcNow <= deadline &&
           !ct.IsCancellationRequested) {
      var available = tcpClient.Available;

      if (available > 0) {
        await ReadChunkAsync(available, ct);
        _written += available;
        consecutiveZeros = 0;
        zeroStartedAt = default;
        await Task.Delay(ReadGracePeriod, ct);
        continue;
      }

      UpdateZeroState(available, ref consecutiveZeros, ref zeroStartedAt);

      if (IsStableIdle(consecutiveZeros, zeroStartedAt)) {
        break;
      }

      await Task.Delay(PollInterval, ct);
    }

    if (DateTime.UtcNow > deadline) {
      throw new TimeoutException("Reading from the stream timed out.");
    }

    var output = new byte[_written];

    _memoryStream.Seek(0, SeekOrigin.Begin);
    _ = await _memoryStream.ReadAsync(output, 0, _written, ct);
    _memoryStream.SetLength(0);
    _written = 0;

    return output;
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _memoryStream.Dispose();
    _disposed = true;
  }

  private async ValueTask ReadChunkAsync(int bytesAvailable, CancellationToken ct) {
    var rentSize = bytesAvailable + DEFAULT_RENT_BUFFER_EXTRA;
    var buffer = ArrayPool<byte>.Shared.Rent(rentSize);

    try {
      var read = await _networkStream.ReadAsync(buffer, 0, bytesAvailable, ct);

      if (read == 0) {
        return;
      }

      _memoryStream.Write(buffer, 0, read);
    }
    finally {
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }

  private void ReadChunk(int bytesAvailable) {
    var rentSize = bytesAvailable + DEFAULT_RENT_BUFFER_EXTRA;
    var buffer = ArrayPool<byte>.Shared.Rent(rentSize);

    try {
      var read = _networkStream.Read(buffer, 0, bytesAvailable);

      if (read == 0) {
        return;
      }

      _memoryStream.Write(buffer, 0, read);
    }
    finally {
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }

  private static void UpdateZeroState(int available, ref int consecutiveZeros, ref DateTime zeroStartedAt) {
    if (available == 0) {
      consecutiveZeros++;

      if (consecutiveZeros == 1) {
        zeroStartedAt = DateTime.UtcNow;
      }

      return;
    }

    consecutiveZeros = 0;
    zeroStartedAt = default;
  }

  private bool IsStableIdle(int consecutiveZeros, DateTime zeroStartedAt) {
    if (consecutiveZeros == 0) {
      return false;
    }

    var elapsed = DateTime.UtcNow - zeroStartedAt;
    return elapsed >= StabilizationDelay;
  }
}
