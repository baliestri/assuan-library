// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Threading.Channels;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class ConcurrentServerTests {
  [Fact]
  public async Task ConfigurationClosesAtFirstServeAndCannotBeReused() {
    var constructed = 0;
    var server = new AssuanServer<object>(() => {
      Interlocked.Increment(ref constructed);
      return new object();
    },
      ServerOptions.Default);
    server.Register(new NoopHandler());
    server.SetHooks(new DefaultSessionHooks<object>());
    using var stop = new CancellationTokenSource();
    await stop.CancelAsync();
    await server.ServeAsync(new ControlledAcceptor(), stop.Token);
    Assert.Equal(0, constructed);
    Assert.Throws<InvalidOperationException>(() => server.Register(new NoopHandler()));
    Assert.Throws<InvalidOperationException>(() => server.SetHooks(new DefaultSessionHooks<object>()));
    await Assert.ThrowsAsync<InvalidOperationException>(() => server.ServeAsync(new ControlledAcceptor(), stop.Token));
  }

  [Fact]
  public void RejectsNullFactoryBeforeTakingAnAcceptor()
    => Assert.Throws<ArgumentNullException>(() => new AssuanServer<object>(null!, ServerOptions.Default));

  [Fact]
  public async Task DisposedHostCannotStartOrBeReconfigured() {
    var server = new AssuanServer<object>(() => new object(), ServerOptions.Default);
    await server.DisposeAsync();
    Assert.Throws<ObjectDisposedException>(() => server.Register(new NoopHandler()));
    Assert.Throws<ObjectDisposedException>(() => {
      _ = server.ServeAsync(new ControlledAcceptor(), TestContext.Current.CancellationToken);
    });
  }

  [Fact]
  public async Task AtCapacityNoSecondAcceptBeginsUntilFirstSessionActuallyFinishes() {
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var acceptor = new ControlledAcceptor();
    var first = new SessionDuplexStream("WAIT\nBYE\n"u8.ToArray());
    var second = new SessionDuplexStream("NOP\nBYE\n"u8.ToArray());
    var server = new AssuanServer<object>(() => new object(), new ServerOptions(maxSessions: 1));
    server.Register(new WaitingHandler(entered, release));
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    acceptor.Enqueue(first);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    acceptor.Enqueue(second);
    Assert.Equal(1, Volatile.Read(ref acceptor._acceptCalls));
    release.SetResult();
    await acceptor.WaitForCallsAsync(2, TestContext.Current.CancellationToken);
    await server.WhenDrainedAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5),
      TestContext.Current.CancellationToken);
    Assert.Equal("OK\nOK\nOK\n"u8.ToArray(), first.WrittenBytes);
    Assert.Equal("OK\nOK\nOK\n"u8.ToArray(), second.WrittenBytes);
    await stop.CancelAsync();
    await serving;
  }

  [Fact]
  public async Task TwoSessionsCanRunConcurrentlyWithIndependentState() {
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var acceptor = new ControlledAcceptor();
    var first = new SessionDuplexStream("WAIT\nBYE\n"u8.ToArray());
    var second = new SessionDuplexStream("BYE\n"u8.ToArray());
    var constructed = 0;
    var server = new AssuanServer<object>(() => {
      Interlocked.Increment(ref constructed);
      return new object();
    },
      new ServerOptions(maxSessions: 2));
    server.Register(new WaitingHandler(entered, release));
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    acceptor.Enqueue(first);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    acceptor.Enqueue(second);
    await acceptor.WaitForCallsAsync(2, TestContext.Current.CancellationToken);
    try {
      await ControlledAcceptor.WaitUntilAsync(() => Volatile.Read(ref constructed) == 2,
        TestContext.Current.CancellationToken);
    }
    finally { release.SetResult(); }

    await server.WhenDrainedAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2),
      TestContext.Current.CancellationToken);
    Assert.Equal("OK\nOK\n"u8.ToArray(), second.WrittenBytes);
    await stop.CancelAsync();
    await serving;
  }

  [Fact]
  public async Task IdleTimeoutReleasesCapacityForTheNextConnection() {
    var acceptor = new ControlledAcceptor();
    var first = new WaitingForIdleStream();
    var second = new SessionDuplexStream("BYE\n"u8.ToArray());
    var server = new AssuanServer<object>(() => new object(),
      new ServerOptions(maxSessions: 1, idleTimeout: TimeSpan.FromMilliseconds(80)));
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    acceptor.Enqueue(first);
    acceptor.Enqueue(second);
    await acceptor.WaitForCallsAsync(3, TestContext.Current.CancellationToken);
    await server.WhenDrainedAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2),
      TestContext.Current.CancellationToken);
    Assert.Equal("OK\n"u8.ToArray(), first.WrittenBytes);
    Assert.Equal("OK\nOK\n"u8.ToArray(), second.WrittenBytes);
    await stop.CancelAsync();
    await serving;
  }

  private sealed class WaitingForIdleStream : SessionDuplexStream {
    internal WaitingForIdleStream() : base(ReadOnlyMemory<byte>.Empty) { }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      return 0;
    }
  }

  private sealed class NoopHandler : ICommandHandler<object> {
    public string Name => "NOOP";
    public string Description => "test";

    public ValueTask HandleAsync(CommandContext<object> context, CancellationToken token)
      => ValueTask.CompletedTask;
  }

  private sealed class WaitingHandler(TaskCompletionSource entered, TaskCompletionSource release)
    : ICommandHandler<object> {
    public string Name => "WAIT";
    public string Description => "wait";

    public async ValueTask HandleAsync(CommandContext<object> context, CancellationToken token) {
      entered.TrySetResult();
      await release.Task; // Deliberately held to exercise permit ownership.
    }
  }
}

internal sealed class ControlledAcceptor : IAssuanAcceptor {
  internal readonly TaskCompletionSource _firstAccept = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private readonly Channel<AcceptedConnection> _queue = Channel.CreateUnbounded<AcceptedConnection>();
  internal int _acceptCalls;
  internal bool _disposed;

  public async ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
    Interlocked.Increment(ref _acceptCalls);
    _firstAccept.TrySetResult();
    return await _queue.Reader.ReadAsync(cancellationToken);
  }

  public ValueTask DisposeAsync() {
    _disposed = true;
    _queue.Writer.TryComplete();
    while (_queue.Reader.TryRead(out var connection)) {
      connection.Stream.Dispose();
    }

    return ValueTask.CompletedTask;
  }

  internal void Enqueue(Stream stream) => _queue.Writer.TryWrite(new AcceptedConnection(stream));

  internal async Task WaitForCallsAsync(int count, CancellationToken token)
    => await WaitUntilAsync(() => Volatile.Read(ref _acceptCalls) >= count, token);

  internal static async Task WaitUntilAsync(Func<bool> ready, CancellationToken token) {
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));
    while (!ready()) {
      await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
    }
  }
}
