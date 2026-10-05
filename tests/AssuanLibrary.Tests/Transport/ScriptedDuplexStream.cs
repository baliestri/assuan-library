// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests.Transport;

internal class ScriptedDuplexStream(byte[] incoming, int maxRead = 1000) : Stream {
  private int _position;
  internal int ReadCalls { get; private set; }
  internal int WriteCalls { get; private set; }
  internal int FlushCalls { get; private set; }
  internal bool Disposed { get; private set; }
  internal bool FailAfterPrefix { get; set; }
  internal List<byte> Written { get; } = [];

  public override bool CanRead => !Disposed;
  public override bool CanWrite => !Disposed;
  public override bool CanSeek => false;
  public override long Length => throw new NotSupportedException();

  public override long Position {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ++ReadCalls;
    var length = Math.Min(Math.Min(buffer.Length, maxRead), incoming.Length - _position);
    incoming.AsSpan(_position, length).CopyTo(buffer.Span);
    _position += length;
    return ValueTask.FromResult(length);
  }

  public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ++WriteCalls;
    if (FailAfterPrefix) {
      Written.Add(buffer.Span[0]);
      throw new IOException("SECRET_MARKER external I/O error");
    }

    Written.AddRange(buffer.ToArray());
    return ValueTask.CompletedTask;
  }

  public override Task FlushAsync(CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    ++FlushCalls;
    return Task.CompletedTask;
  }

  public override void Flush() => ++FlushCalls;
  public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
  public override void SetLength(long value) => throw new NotSupportedException();
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  protected override void Dispose(bool disposing) {
    Disposed = true;
    base.Dispose(disposing);
  }
}
