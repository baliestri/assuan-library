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
/// <param name="pipeStream">The named pipe client to read from.</param>
/// <param name="timeoutInMilliseconds">The maximum duration to wait for the stream to stabilize before timing out.</param>
/// <param name="options">The stabilization options to use.</param>
[SupportedOSPlatform("windows")]
public struct StabilizedNamedPipeReader(PipeStream pipeStream, int timeoutInMilliseconds, StabilizationOptions options)
  : IStabilizedReader {
  private const int INITIAL_BUFFER_CAPACITY = 4096;
  private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(timeoutInMilliseconds);
  private readonly MemoryStream _memoryStream = new(INITIAL_BUFFER_CAPACITY);
  private bool _disposed;

  /// <summary>
  ///   Initializes a new instance of the <see cref="StabilizedNamedPipeReader" /> struct with default stabilization options.
  /// </summary>
  /// <param name="pipeStream">The named pipe client to read from.</param>
  /// <param name="timeoutInMilliseconds">The maximum duration to wait for the stream to stabilize before timing out.</param>
  public StabilizedNamedPipeReader(PipeStream pipeStream, int timeoutInMilliseconds)
    : this(pipeStream, timeoutInMilliseconds, StabilizationOptions.Default) { }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedNamedPipeReader));

    var sw = Stopwatch.StartNew();
    var lastTick = sw.Elapsed;

    var idleElapsed = TimeSpan.Zero;
    var idleCycles = 0;

    var hasReceivedAnyData = false;

    var buffer = ArrayPool<byte>.Shared.Rent(INITIAL_BUFFER_CAPACITY);

    try {
      while (true) {
        if (!pipeStream.IsConnected) {
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

        if (pipeStream.CanRead) {
          var read = pipeStream.Read(buffer, 0, buffer.Length);
          hadData = read > 0;

          if (hadData) {
            hasReceivedAnyData = true;
            _memoryStream.Write(buffer, 0, read);
            Array.Clear(buffer, 0, read);
          }
        }

        if (pipeStream.IsMessageComplete) {
          break;
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
    ObjectDisposedException.ThrowIf(_disposed, nameof(StabilizedNamedPipeReader));

    var sw = Stopwatch.StartNew();
    var lastTick = sw.Elapsed;

    var idleElapsed = TimeSpan.Zero;
    var idleCycles = 0;

    var hasReceivedAnyData = false;

    var buffer = ArrayPool<byte>.Shared.Rent(INITIAL_BUFFER_CAPACITY);

    Console.WriteLine($"DEBUG: Starting stabilized read with timeout {_timeout.TotalMilliseconds} ms.");

    try {
      while (!ct.IsCancellationRequested) {
        if (!pipeStream.IsConnected) {
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

        Console.WriteLine($"DEBUG: CanRead = {pipeStream.CanRead}, InBufferSize = {pipeStream.InBufferSize}");

        if (pipeStream.CanRead) {
          var read = await pipeStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
          hadData = read > 0;

          if (hadData) {
            Console.WriteLine($"DEBUG: Read {read} bytes from the named pipe.");

            hasReceivedAnyData = true;
            _memoryStream.Write(buffer, 0, read);
            Array.Clear(buffer, 0, read);
          }

          if (pipeStream.IsMessageComplete) {
            break;
          }
        }

        StabilizationIdleDetector.Update(hadData, elapsed, ref idleElapsed, ref idleCycles);

        if (hasReceivedAnyData && StabilizationIdleDetector.IsStable(idleElapsed, options.Delay)) {
          Console.WriteLine($"DEBUG -- Data has stabilized after {sw.Elapsed.TotalMilliseconds} ms.");
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
