// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class SessionDeadlineTests {
  [Fact]
  public async Task TimeoutClosesChannelButWaitsForNoncooperativeHandlerBeforeCloseHook() {
    await using var stream = new SessionDuplexStream("WAIT private\n"u8.ToArray());
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var registry = new CommandRegistry<object>();
    var handler = new Handler(release, started);
    registry.Register(handler);
    var hooks = new Hooks();
    var session = new ServerSession<object>(new AcceptedConnection(stream), new object(), registry,
      hooks, new ServerOptions(commandTimeout: TimeSpan.FromMilliseconds(100)));
    var run = session.RunAsync(TestContext.Current.CancellationToken);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    try {
      await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
      Assert.False(run.IsCompleted);
      Assert.Equal(0, hooks._closed);
      Assert.Equal("private"u8.ToArray(), handler.BorrowedArguments.ToArray());
    }
    finally { release.SetResult(); }

    var error = await Assert.ThrowsAsync<ServerException>(() => run);
    Assert.Equal(ServerErrorKind.Timeout, error.Kind);
    Assert.Equal(1, hooks._closed);
    Assert.Equal(new byte[7], handler.BorrowedArguments.ToArray());
    Assert.DoesNotContain("private", error.ToString());
    Assert.Equal("OK\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task HostStopDoesNotReleaseStateWhileHandlerIgnoresCancellation() {
    await using var stream = new SessionDuplexStream("WAIT private\n"u8.ToArray());
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var registry = new CommandRegistry<object>();
    registry.Register(new Handler(release, started));
    var hooks = new Hooks();
    var session = new ServerSession<object>(new AcceptedConnection(stream), new object(), registry,
      hooks, ServerOptions.Default);
    var run = session.RunAsync(TestContext.Current.CancellationToken);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    session.Control.RequestStop();
    try {
      Assert.False(run.IsCompleted);
      Assert.Equal(0, hooks._closed);
    }
    finally { release.SetResult(); }

    var error = await Assert.ThrowsAsync<ServerException>(() => run);
    Assert.Equal(ServerErrorKind.Cancelled, error.Kind);
    Assert.Equal(1, hooks._closed);
  }

  private sealed class Handler(TaskCompletionSource release, TaskCompletionSource started)
    : ICommandHandler<object> {
    internal ReadOnlyMemory<byte> BorrowedArguments { get; private set; }
    public string Name => "WAIT";
    public string Description => "wait";

    public async ValueTask HandleAsync(CommandContext<object> context, CancellationToken cancellationToken) {
      BorrowedArguments = context.Arguments;
      started.SetResult();
      await release.Task; // Deliberately ignores cancellation to test retained ownership.
    }
  }

  private sealed class Hooks : ISessionHooks<object> {
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
