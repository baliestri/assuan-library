// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests.Transport;

internal sealed class NonCooperativeStream : ScriptedDuplexStream {
  private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly TaskCompletionSource _write = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private Memory<byte> _readBuffer;
  private ReadOnlyMemory<byte> _writeBuffer;

  internal TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
  internal TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
  internal ReadOnlyMemory<byte> PendingWrite => _writeBuffer;

  internal NonCooperativeStream() : base([]) { }

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
    _readBuffer = buffer;
    _readBuffer.Span[0] = (byte)'s'; // Secret data may arrive before the task completes.
    ReadStarted.TrySetResult();
    return new ValueTask<int>(_read.Task); // Ignores cancellation and Dispose.
  }

  public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
    _writeBuffer = buffer;
    WriteStarted.TrySetResult();
    return new ValueTask(_write.Task); // Still borrows the caller's buffer after timeout.
  }

  internal void ReleaseRead(ReadOnlySpan<byte> bytes) {
    bytes.CopyTo(_readBuffer.Span);
    _read.TrySetResult(bytes.Length);
  }

  internal void ReleaseWrite() => _write.TrySetResult();
}
