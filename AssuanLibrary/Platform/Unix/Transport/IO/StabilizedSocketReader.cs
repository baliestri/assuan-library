// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using AssuanLibrary.Platform.Unix.Polyfills;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Unix.Transport.IO;

/// <summary>
///   A reader that reads data from a socket and ensures that the data stream has stabilized, indicating that no more data is incoming.
/// </summary>
/// <param name="socket">The socket to read from.</param>
/// <param name="timeoutInMilliseconds">The maximum duration to wait for the socket to stabilize before timing out.</param>
/// <param name="options">The stabilization options to use.</param>
public struct StabilizedSocketReader(Socket socket, int timeoutInMilliseconds, StabilizationOptions options) : IStabilizedReader {
  private const int INITIAL_BUFFER_CAPACITY = 4096;
  private const int DEFAULT_RENT_BUFFER_EXTRA = 32;
  private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(timeoutInMilliseconds);
  private readonly MemoryStream _memoryStream = new(INITIAL_BUFFER_CAPACITY);
  private bool _disposed;

  /// <summary>
  ///   Initializes a new instance of the <see cref="StabilizedSocketReader" /> struct with default stabilization options.
  /// </summary>
  /// <param name="socket">The socket to read from.</param>
  /// <param name="timeoutInMilliseconds">The maximum duration to wait for the socket to stabilize before timing out.</param>
  public StabilizedSocketReader(Socket socket, int timeoutInMilliseconds) : this(socket, timeoutInMilliseconds, StabilizationOptions.Default) { }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedSocketReader));

    var sw = Stopwatch.StartNew();
    var lastTick = sw.Elapsed;

    var idleElapsed = TimeSpan.Zero;
    var idleCycles = 0;

    var hasReceivedAnyData = false;

    while (true) {
      var now = sw.Elapsed;
      var elapsed = now - lastTick;
      lastTick = now;

      var remaining = _timeout - now;
      if (remaining <= TimeSpan.Zero) {
        throw new TimeoutException("Reading from the stream timed out.");
      }

      var available = socket.Available;
      var hadData = false;

      if (available > 0) {
        var read = ReadChunk(available);
        hadData = read > 0;

        if (hadData) {
          hasReceivedAnyData = true;
        }
      }

      StabilizationIdleDetector.Update(hadData, elapsed, ref idleElapsed, ref idleCycles);

      if (hasReceivedAnyData && StabilizationIdleDetector.IsStable(idleElapsed, options.Delay)) {
        break;
      }

      var poll = (int)Math.Min(options.PollInterval.TotalMilliseconds, remaining.TotalMilliseconds);
      Thread.Sleep(poll);
    }

    var output = _memoryStream.ToArray();

    _memoryStream.Seek(0, SeekOrigin.Begin);
    _memoryStream.SetLength(0);

    return output;
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedSocketReader));

    var sw = Stopwatch.StartNew();
    var lastTick = sw.Elapsed;

    var idleElapsed = TimeSpan.Zero;
    var idleCycles = 0;

    var hasReceivedAnyData = false;

    while (!ct.IsCancellationRequested) {
      var now = sw.Elapsed;
      var elapsed = now - lastTick;
      lastTick = now;

      var remaining = _timeout - now;
      if (remaining <= TimeSpan.Zero) {
        throw new TimeoutException("Reading from the stream timed out.");
      }

      var available = socket.Available;
      var hadData = false;

      if (available > 0) {
        var read = await ReadChunkAsync(available, ct).ConfigureAwait(false);
        hadData = read > 0;

        if (hadData) {
          hasReceivedAnyData = true;
        }
      }

      StabilizationIdleDetector.Update(hadData, elapsed, ref idleElapsed, ref idleCycles);

      if (hasReceivedAnyData && StabilizationIdleDetector.IsStable(idleElapsed, options.Delay)) {
        break;
      }

      var poll = (int)Math.Min(options.PollInterval.TotalMilliseconds, remaining.TotalMilliseconds);
      await Task.Delay(poll, ct);
    }

    var output = _memoryStream.ToArray();

    _memoryStream.Seek(0, SeekOrigin.Begin);
    _memoryStream.SetLength(0);

    return output;
  }

  private int ReadChunk(int bytesAvailable) {
    var rentSize = bytesAvailable + DEFAULT_RENT_BUFFER_EXTRA;
    var buffer = ArrayPool<byte>.Shared.Rent(rentSize);

    try {
      var read = socket.Receive(buffer, 0, bytesAvailable, SocketFlags.None);

      if (read == 0) {
        return 0;
      }

      _memoryStream.Write(buffer, 0, read);
      return read;
    }
    finally {
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }

  private async ValueTask<int> ReadChunkAsync(int bytesAvailable, CancellationToken ct) {
    var rentSize = bytesAvailable + DEFAULT_RENT_BUFFER_EXTRA;
    var buffer = ArrayPool<byte>.Shared.Rent(rentSize);

    try {
      var segment = new ArraySegment<byte>(buffer);
      var read = await socket.ReceiveAsync(segment, SocketFlags.None, ct).ConfigureAwait(false);

      if (read == 0) {
        return 0;
      }

      _memoryStream.Write(buffer, 0, read);

      return read;
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
