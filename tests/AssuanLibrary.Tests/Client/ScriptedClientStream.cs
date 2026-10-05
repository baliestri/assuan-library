// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests.Client;

internal class ScriptedClientStream : Stream {
  private readonly MemoryStream _output = new();
  private readonly Queue<byte[]> _replies;
  private MemoryStream _input;

  internal ScriptedClientStream(ReadOnlyMemory<byte> greeting, params byte[][] commandReplies) {
    _input = new MemoryStream(greeting.ToArray(), false);
    _replies = new Queue<byte[]>(commandReplies);
  }

  internal byte[] WrittenBytes => _output.ToArray();
  internal bool Closed { get; private set; }
  public override bool CanRead => !Closed;
  public override bool CanWrite => !Closed;
  public override bool CanSeek => false;
  public override long Length => throw new NotSupportedException();
  public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

  public override int Read(byte[] buffer, int offset, int count)
    => _input.Read(buffer, offset, count);

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    => _input.ReadAsync(buffer, cancellationToken);

  public override void Write(byte[] buffer, int offset, int count)
    => _output.Write(buffer, offset, count);

  public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
    if (_replies.TryDequeue(out var reply)) {
      _input.Dispose();
      _input = new MemoryStream(reply, false);
    }

    return _output.WriteAsync(buffer, cancellationToken);
  }

  public override void Flush()
    => _output.Flush();

  public override Task FlushAsync(CancellationToken cancellationToken)
    => _output.FlushAsync(cancellationToken);

  public override long Seek(long offset, SeekOrigin origin)
    => throw new NotSupportedException();

  public override void SetLength(long value)
    => throw new NotSupportedException();

  protected override void Dispose(bool disposing) {
    if (disposing) {
      Closed = true;
      _input.Dispose();
      _output.Dispose();
    }

    base.Dispose(disposing);
  }
}
