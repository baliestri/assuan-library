// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Threading.Channels;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

internal sealed class ControlledAcceptor : IAssuanAcceptor {
  private readonly TaskCompletionSource _firstAccept = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly Channel<AcceptedConnection> _queue = Channel.CreateUnbounded<AcceptedConnection>();
  internal int AcceptCalls;

  public async ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
    Interlocked.Increment(ref AcceptCalls);
    _firstAccept.TrySetResult();
    return await _queue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
  }

  public ValueTask DisposeAsync() {
    _queue.Writer.TryComplete();
    while (_queue.Reader.TryRead(out var connection)) connection.Stream.Dispose();

    return ValueTask.CompletedTask;
  }

  internal void Enqueue(Stream stream) => _queue.Writer.TryWrite(new AcceptedConnection(stream));

  internal async Task WaitForCallsAsync(int count, CancellationToken token)
    => await WaitUntilAsync(() => Volatile.Read(ref AcceptCalls) >= count, token).ConfigureAwait(false);

  internal static async Task WaitUntilAsync(Func<bool> ready, CancellationToken token) {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));
    while (!ready()) await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token).ConfigureAwait(false);
  }
}
