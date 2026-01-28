// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Windows.Transport.IO;

/// <summary>
///   A reader that reads from a named pipe client until the data stabilizes, indicating no more data is incoming.
/// </summary>
/// <param name="namedPipeClientStream">The named pipe client to read from.</param>
/// <param name="timeoutInMilliseconds">The maximum duration to wait for the stream to stabilize before timing out.</param>
/// <param name="options">The stabilization options to use.</param>
[SupportedOSPlatform("windows")]
public struct StabilizedNamedPipeClientReader(NamedPipeClientStream namedPipeClientStream, int timeoutInMilliseconds, StabilizationOptions options)
  : IStabilizedReader {
  private const int INITIAL_BUFFER_CAPACITY = 4096;
  private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(timeoutInMilliseconds);
  private readonly MemoryStream _memoryStream = new(INITIAL_BUFFER_CAPACITY);
  private bool _disposed;

  /// <summary>
  ///   Initializes a new instance of the <see cref="StabilizedNamedPipeClientReader" /> struct with default stabilization options.
  /// </summary>
  /// <param name="namedPipeClientStream">The named pipe client to read from.</param>
  /// <param name="timeoutInMilliseconds">The maximum duration to wait for the stream to stabilize before timing out.</param>
  public StabilizedNamedPipeClientReader(NamedPipeClientStream namedPipeClientStream, int timeoutInMilliseconds)
    : this(namedPipeClientStream, timeoutInMilliseconds, StabilizationOptions.Default) { }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedNamedPipeClientReader));

    var sw = Stopwatch.StartNew();
    var lastTick = sw.Elapsed;

    var idleElapsed = TimeSpan.Zero;
    var idleCycles = 0;

    var hasReceivedAnyData = false;

    var buffer = ArrayPool<byte>.Shared.Rent(INITIAL_BUFFER_CAPACITY);

    try {
      while (true) {
        if (!namedPipeClientStream.IsConnected) {
          break;
        }

        var now = sw.Elapsed;
        var elapsed = now - lastTick;
        lastTick = now;

        var remaining = _timeout - now;
        if (remaining <= TimeSpan.Zero) {
          throw new TimeoutException("Reading from the stream timed out.");
        }

        var hadData = false;

        if (namedPipeClientStream.CanRead) {
          var read = namedPipeClientStream.Read(buffer, 0, buffer.Length);
          hadData = read > 0;

          if (hadData) {
            hasReceivedAnyData = true;
            _memoryStream.Write(buffer, 0, read);
            Array.Clear(buffer, 0, read);

            if (namedPipeClientStream.IsMessageComplete) {
              break;
            }
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
    finally {
      ArrayPool<byte>.Shared.Return(buffer, true);
    }
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedNamedPipeClientReader));

    var sw = Stopwatch.StartNew();
    var lastTick = sw.Elapsed;

    var idleElapsed = TimeSpan.Zero;
    var idleCycles = 0;

    var hasReceivedAnyData = false;

    var buffer = ArrayPool<byte>.Shared.Rent(INITIAL_BUFFER_CAPACITY);

    try {
      while (!ct.IsCancellationRequested) {
        if (!namedPipeClientStream.IsConnected) {
          break;
        }

        var now = sw.Elapsed;
        var elapsed = now - lastTick;
        lastTick = now;

        var remaining = _timeout - now;
        if (remaining <= TimeSpan.Zero) {
          throw new TimeoutException("Reading from the stream timed out.");
        }

        var hadData = false;

        if (namedPipeClientStream.CanRead) {
          var read = await namedPipeClientStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
          hadData = read > 0;

          if (hadData) {
            hasReceivedAnyData = true;
            _memoryStream.Write(buffer, 0, read);
            Array.Clear(buffer, 0, read);

            if (namedPipeClientStream.IsMessageComplete) {
              break;
            }
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
