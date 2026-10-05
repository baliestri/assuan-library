// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests.Server;

internal class SessionDuplexStream(ReadOnlyMemory<byte> input) : Stream {
  private readonly MemoryStream _input = new(input.ToArray(), false);
  private readonly MemoryStream _output = new();
  internal byte[] WrittenBytes => _output.ToArray();
  public override bool CanRead => true;
  public override bool CanWrite => true;
  public override bool CanSeek => false;
  public override long Length => throw new NotSupportedException();
  public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

  public override void Flush()
    => _output.Flush();

  public override Task FlushAsync(CancellationToken cancellationToken)
    => _output.FlushAsync(cancellationToken);

  public override int Read(byte[] buffer, int offset, int count)
    => _input.Read(buffer, offset, count);

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    => _input.ReadAsync(buffer, cancellationToken);

  public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

  public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    => _output.WriteAsync(buffer, cancellationToken);

  public override long Seek(long offset, SeekOrigin origin)
    => throw new NotSupportedException();

  public override void SetLength(long value)
    => throw new NotSupportedException();

  protected override void Dispose(bool disposing) {
    if (disposing) {
      _input.Dispose();
      _output.Dispose();
    }

    base.Dispose(disposing);
  }
}
