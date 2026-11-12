// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Threading.Channels;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Examples;

internal sealed class SingleConnectionAcceptor(Stream stream) : IAssuanAcceptor {
  private readonly Channel<AcceptedConnection> _queue = CreateQueue(stream);
  private int _disposed;

  public ValueTask<AcceptedConnection> AcceptAsync(CancellationToken token = default)
    => _queue.Reader.ReadAsync(token);

  public async ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0) {
      _queue.Writer.TryComplete();
      while (_queue.Reader.TryRead(out var connection))
        await connection.Stream.DisposeAsync().ConfigureAwait(false);
    }
  }

  private static Channel<AcceptedConnection> CreateQueue(Stream stream) {
    var queue = Channel.CreateUnbounded<AcceptedConnection>();
    queue.Writer.TryWrite(new AcceptedConnection(stream));
    return queue;
  }
}
