// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class ServerShutdownTests {
  [Fact]
  public async Task ShutdownReturnsAfterOneControlledGraceButKeepsUnfinishedSessionsOwned() {
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var graceStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var endGrace = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var delayCalls = 0;
    var acceptor = new ControlledAcceptor();
    var hooks = new CloseHooks();
    var server = new AssuanServer<object>(() => new object(), ServerOptions.Default, (duration, _) => {
      Assert.Equal(TimeSpan.FromSeconds(30), duration);
      Interlocked.Increment(ref delayCalls);
      graceStarted.TrySetResult();
      return endGrace.Task;
    });
    server.Register(new WaitHandler(started, release));
    server.SetHooks(hooks);
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    acceptor.Enqueue(new SessionDuplexStream("WAIT private\n"u8.ToArray()));
    await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    await stop.CancelAsync();
    await graceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.False(serving.IsCompleted);
    Assert.Equal(1, server.PendingSessionCount);
    Assert.Equal(0, hooks._closed);
    endGrace.SetResult();
    await serving.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(1, delayCalls);
    Assert.Equal(1, server.PendingSessionCount);
    using var cancelled = new CancellationTokenSource();
    await cancelled.CancelAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.WhenDrainedAsync(cancelled.Token));
    Assert.Equal(1, server.PendingSessionCount);
    release.SetResult();
    await server.WhenDrainedAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2),
      TestContext.Current.CancellationToken);
    Assert.Equal(0, server.PendingSessionCount);
    Assert.Equal(1, hooks._closed);
  }

  [Fact]
  public async Task BothSessionsShareOneGraceAndReleaseTheirPermitsOnlyOnCompletion() {
    var started = 0;
    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var grace = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var graceCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var delays = 0;
    var acceptor = new ControlledAcceptor();
    var server = new AssuanServer<object>(() => new object(), new ServerOptions(maxSessions: 2),
      (_, _) => {
        Interlocked.Increment(ref delays);
        graceCalled.TrySetResult();
        return grace.Task;
      });
    server.Register(new WaitHandler(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), gate,
      () => Interlocked.Increment(ref started)));
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    acceptor.Enqueue(new SessionDuplexStream("WAIT\n"u8.ToArray()));
    acceptor.Enqueue(new SessionDuplexStream("WAIT\n"u8.ToArray()));
    await ControlledAcceptor.WaitUntilAsync(() => Volatile.Read(ref started) == 2,
      TestContext.Current.CancellationToken);
    await stop.CancelAsync();
    await graceCalled.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    grace.SetResult();
    await serving.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(1, delays);
    Assert.Equal(2, server.PendingSessionCount);
    gate.SetResult();
    await server.WhenDrainedAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2),
      TestContext.Current.CancellationToken);
    Assert.Equal(0, server.PendingSessionCount);
  }

  [Fact]
  public async Task LoopbackPeerCanSendByeDuringGraceAndHostReturnsEarly() {
    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    var graceCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var holdGrace = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var server = new AssuanServer<object>(() => new object(), ServerOptions.Default,
      (_, _) => {
        graceCalled.TrySetResult();
        return holdGrace.Task;
      });
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(listener, stop.Token);
    using var peer = new TcpClient(AddressFamily.InterNetwork);
    var token = TestContext.Current.CancellationToken;
    await peer.ConnectAsync(listener.Endpoint.Address, listener.Endpoint.Port, token);
    var stream = peer.GetStream();
    var greeting = new byte[3];
    await stream.ReadExactlyAsync(greeting, token);
    Assert.Equal("OK\n"u8.ToArray(), greeting);
    await stop.CancelAsync();
    await graceCalled.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
    await stream.WriteAsync("BYE\n"u8.ToArray(), token);
    var response = new byte[3];
    await stream.ReadExactlyAsync(response, token);
    Assert.Equal("OK\n"u8.ToArray(), response);
    await serving.WaitAsync(TimeSpan.FromSeconds(2), token);
    Assert.Equal(0, server.PendingSessionCount);
    Assert.False(holdGrace.Task.IsCompleted);
  }

  private sealed class WaitHandler(TaskCompletionSource started, TaskCompletionSource release, Action? onStart = null)
    : ICommandHandler<object> {
    public string Name => "WAIT";
    public string Description => "wait";

    public async ValueTask HandleAsync(CommandContext<object> context, CancellationToken token) {
      onStart?.Invoke();
      started.TrySetResult();
      await release.Task; // Deliberately noncooperative.
    }
  }

  private sealed class CloseHooks : ISessionHooks<object> {
    internal int _closed;

    public ValueTask AuthenticateAsync(CommandContext<object> context, CancellationToken token)
      => ValueTask.CompletedTask;

    public ValueTask OptionAsync(OptionRequest request, CommandContext<object> context, CancellationToken token)
      => ValueTask.CompletedTask;

    public ValueTask ResetAsync(CommandContext<object> context, CancellationToken token)
      => ValueTask.CompletedTask;

    public ValueTask ClosedAsync(object state, SessionEnd reason, CancellationToken token) {
      Interlocked.Increment(ref _closed);
      return ValueTask.CompletedTask;
    }
  }
}
