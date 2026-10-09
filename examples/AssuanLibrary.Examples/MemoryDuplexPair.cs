// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Threading.Channels;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Examples;

internal static class MemoryDuplexPair {
  internal static (Stream Client, Stream Server) Create() {
    var toClient = Channel.CreateUnbounded<byte[]>();
    var toServer = Channel.CreateUnbounded<byte[]>();
    return (new DuplexStream(toClient.Reader, toServer.Writer),
      new DuplexStream(toServer.Reader, toClient.Writer));
  }

  private sealed class DuplexStream(ChannelReader<byte[]> incoming, ChannelWriter<byte[]> outgoing) : Stream {
    private readonly CancellationTokenSource _closed = new();
    private int _disposed;
    private int _offset;
    private byte[]? _pending;
    public override bool CanRead => Volatile.Read(ref _disposed) == 0;
    public override bool CanWrite => Volatile.Read(ref _disposed) == 0;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }

    public override Task FlushAsync(CancellationToken token) {
      token.ThrowIfCancellationRequested();
      return Task.CompletedTask;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) {
      if (buffer.IsEmpty) {
        return 0;
      }

      using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _closed.Token);
      while (_pending is null ||
             _offset == _pending.Length) {
        try {
          if (!await incoming.WaitToReadAsync(linked.Token).ConfigureAwait(false)) {
            return 0;
          }
        }
        catch (OperationCanceledException) when (_closed.IsCancellationRequested) { return 0; }

        if (!incoming.TryRead(out var next)) {
          continue;
        }

        _pending = next;
        _offset = 0;
      }

      var count = Math.Min(buffer.Length, _pending.Length - _offset);
      _pending.AsMemory(_offset, count).CopyTo(buffer);
      _offset += count;
      if (_offset == _pending.Length) {
        CryptographicOperations.ZeroMemory(_pending);
        _pending = null;
      }

      return count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) {
      ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
      var copy = buffer.ToArray();
      try { await outgoing.WriteAsync(copy, token).ConfigureAwait(false); }
      catch {
        CryptographicOperations.ZeroMemory(copy);
        throw;
      }
    }

    public override int Read(byte[] buffer, int offset, int count)
      => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override void Write(byte[] buffer, int offset, int count)
      => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing) {
      if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) {
        outgoing.TryComplete();
        _closed.Cancel();
        _closed.Dispose();
        if (_pending is not null) {
          CryptographicOperations.ZeroMemory(_pending);
        }
      }

      base.Dispose(disposing);
    }
  }
}

internal sealed class SingleConnectionAcceptor(Stream stream) : IAssuanAcceptor {
  private readonly Channel<AcceptedConnection> _queue = CreateQueue(stream);
  private int _disposed;

  public ValueTask<AcceptedConnection> AcceptAsync(CancellationToken token = default)
    => _queue.Reader.ReadAsync(token);

  public ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0) {
      _queue.Writer.TryComplete();
      while (_queue.Reader.TryRead(out var connection)) {
        connection.Stream.Dispose();
      }
    }

    return ValueTask.CompletedTask;
  }

  private static Channel<AcceptedConnection> CreateQueue(Stream stream) {
    var queue = Channel.CreateUnbounded<AcceptedConnection>();
    queue.Writer.TryWrite(new AcceptedConnection(stream));
    return queue;
  }
}
