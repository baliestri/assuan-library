// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;

namespace AssuanLibrary.Transport.Windows;

/// <summary>Byte-mode pipe adapter whose flush never waits for the peer to consume bytes.</summary>
internal sealed class PipeByteStream(PipeStream stream) : Stream {
  private PipeStream? _stream = stream;

  private PipeStream Inner => _stream ?? throw new ObjectDisposedException(nameof(PipeByteStream));

  public override bool CanRead => _stream?.CanRead ?? false;
  public override bool CanWrite => _stream?.CanWrite ?? false;
  public override bool CanSeek => false;
  public override long Length => throw new NotSupportedException();

  public override long Position {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    => Inner.ReadAsync(buffer, cancellationToken);

  public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    => Inner.WriteAsync(buffer, cancellationToken);

  public override int Read(byte[] buffer, int offset, int count)
    => Inner.Read(buffer, offset, count);

  public override void Write(byte[] buffer, int offset, int count)
    => Inner.Write(buffer, offset, count);

  public override void Flush()
    => _ = Inner;

  public override Task FlushAsync(CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    _ = Inner;
    return Task.CompletedTask;
  }

  public override long Seek(long offset, SeekOrigin origin)
    => throw new NotSupportedException();

  public override void SetLength(long value)
    => throw new NotSupportedException();

  protected override void Dispose(bool disposing) {
    if (disposing) {
      Interlocked.Exchange(ref _stream, null)?.Dispose();
    }

    base.Dispose(disposing);
  }

  public override ValueTask DisposeAsync() {
    Dispose();
    return ValueTask.CompletedTask;
  }

  public override string ToString()
    => "PipeByteStream { .. }";
}
