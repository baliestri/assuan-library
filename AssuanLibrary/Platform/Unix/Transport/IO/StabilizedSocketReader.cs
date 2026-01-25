// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Net.Sockets;
using AssuanLibrary.Platform.Unix.Polyfills;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Unix.Transport.IO;

/// <summary>
///   A reader that reads data from a socket and ensures that the data stream has stabilized, indicating that no more data is incoming.
/// </summary>
/// <param name="socket">The socket to read from.</param>
/// <param name="timeout">The maximum duration to wait for the socket to stabilize before timing out.</param>
/// <param name="options">The stabilization options to use.</param>
public struct StabilizedSocketReader(Socket socket, TimeSpan timeout, StabilizationOptions options) : IStabilizedReader {
  private const int INITIAL_BUFFER_CAPACITY = 4096;
  private const int DEFAULT_RENT_BUFFER_EXTRA = 32;
  private readonly MemoryStream _memoryStream = new(INITIAL_BUFFER_CAPACITY);
  private int _written;
  private bool _disposed;

  /// <summary>
  ///   Initializes a new instance of the <see cref="StabilizedSocketReader" /> struct with default stabilization options.
  /// </summary>
  /// <param name="socket">The socket to read from.</param>
  /// <param name="timeout">The maximum duration to wait for the socket to stabilize before timing out.</param>
  public StabilizedSocketReader(Socket socket, TimeSpan timeout) : this(socket, timeout, StabilizationOptions.Default) { }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedSocketReader));

    var deadline = DateTime.UtcNow + timeout;
    var consecutiveZeros = 0;
    var zeroStartedAt = default(DateTime);

    while (DateTime.UtcNow <= deadline) {
      var available = socket.Available;

      if (available > 0) {
        ReadChunk(available);
        _written += available;
        consecutiveZeros = 0;
        zeroStartedAt = default;
        Thread.Sleep(options.GracePeriod);
        continue;
      }

      StabilizationIdleDetector.UpdateZeroState(available, ref consecutiveZeros, ref zeroStartedAt);

      if (StabilizationIdleDetector.IsStableIdle(consecutiveZeros, zeroStartedAt, options.Delay)) {
        break;
      }

      Thread.Sleep(options.PollInterval);
    }

    if (DateTime.UtcNow > deadline) {
      throw new TimeoutException("Reading from socket timed out.");
    }

    var output = new byte[_written];
    _memoryStream.Seek(0, SeekOrigin.Begin);
    _ = _memoryStream.Read(output, 0, _written);
    _memoryStream.SetLength(0);
    _written = 0;

    return output;
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedSocketReader));

    var deadline = DateTime.UtcNow + timeout;
    var consecutiveZeros = 0;
    var zeroStartedAt = default(DateTime);

    while (DateTime.UtcNow <= deadline &&
           !ct.IsCancellationRequested) {
      var available = socket.Available;

      if (available > 0) {
        await ReadChunkAsync(available, ct);
        _written += available;
        consecutiveZeros = 0;
        zeroStartedAt = default;
        await Task.Delay(options.GracePeriod, ct);
        continue;
      }

      StabilizationIdleDetector.UpdateZeroState(available, ref consecutiveZeros, ref zeroStartedAt);

      if (StabilizationIdleDetector.IsStableIdle(consecutiveZeros, zeroStartedAt, options.Delay)) {
        break;
      }

      await Task.Delay(options.PollInterval, ct);
    }

    if (DateTime.UtcNow > deadline) {
      throw new TimeoutException("Reading from socket timed out.");
    }

    var output = new byte[_written];
    _memoryStream.Seek(0, SeekOrigin.Begin);
    await _memoryStream.ReadAsync(output.AsMemory(0, _written), ct);
    _memoryStream.SetLength(0);
    _written = 0;

    return output;
  }

  private void ReadChunk(int bytesAvailable) {
    var rentSize = bytesAvailable + DEFAULT_RENT_BUFFER_EXTRA;
    var buffer = ArrayPool<byte>.Shared.Rent(rentSize);

    try {
      var read = socket.Receive(buffer, 0, bytesAvailable, SocketFlags.None);

      if (read == 0) {
        return;
      }

      _memoryStream.Write(buffer, 0, read);
    }
    finally {
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }

  private async ValueTask ReadChunkAsync(int bytesAvailable, CancellationToken ct) {
    var rentSize = bytesAvailable + DEFAULT_RENT_BUFFER_EXTRA;
    var buffer = ArrayPool<byte>.Shared.Rent(rentSize);

    try {
      var segment = new ArraySegment<byte>(buffer);
      var read = await socket.ReceiveAsync(segment, SocketFlags.None, ct);

      if (read == 0) {
        return;
      }

      _memoryStream.Write(buffer, 0, read);
    }
    finally {
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _memoryStream.Dispose();
    _disposed = true;
  }
}
