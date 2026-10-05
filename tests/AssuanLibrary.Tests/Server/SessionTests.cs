// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class SessionTests {
  [Fact]
  public async Task GreetingAndSerialBuiltinsSendExactlyOneFinalEach() {
    await using var stream = new SessionDuplexStream("NOP\nBYE\n"u8.ToArray());
    var registry = new CommandRegistry<Counter>();
    var session = new ServerSession<Counter>(new AcceptedConnection(stream), new Counter(), registry,
      new DefaultSessionHooks<Counter>(), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal("OK\nOK\nOK\n"u8.ToArray(), stream.WrittenBytes);
    await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ArgumentsSurviveAwaitAndRemoteErrorsPermitFollowingCommand() {
    await using var stream = new SessionDuplexStream("ECHO %00\nFAIL\nECHO again\nBYE\n"u8.ToArray());
    var registry = new CommandRegistry<Counter>();
    registry.Register(new Handler("ECHO", async (context, token) => {
      await Task.Yield();
      context.State._value++;
      await context.SendDataAsync(context.Arguments, token);
    }));
    registry.Register(new Handler("FAIL", (_, _) => ValueTask.FromException(HandlerException.Remote(55, "public error"))));
    var state = new Counter();
    var session = new ServerSession<Counter>(new AcceptedConnection(stream), state, registry,
      new DefaultSessionHooks<Counter>(), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, state._value);
    Assert.Equal("OK\nD %2500\nOK\nERR 55 public error\nD again\nOK\nOK\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task UnknownAndInternalErrorsRemainRecoverableButDoNotLeakDetails() {
    await using var stream = new SessionDuplexStream("MISSING\nFAIL\nNOP\nBYE\n"u8.ToArray());
    var registry = new CommandRegistry<Counter>();
    registry.Register(new Handler("FAIL", (_, _) => ValueTask.FromException(new IOException("secret detail"))));
    var session = new ServerSession<Counter>(new AcceptedConnection(stream), new Counter(), registry,
      new DefaultSessionHooks<Counter>(), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    var wire = Encoding.UTF8.GetString(stream.WrittenBytes);
    Assert.Contains("ERR 175 unknown command\n", wire);
    Assert.Contains("ERR 63 internal error\n", wire);
    Assert.EndsWith("OK\nOK\n", wire);
    Assert.DoesNotContain("secret detail", wire);
  }

  [Fact]
  public async Task AuthenticationPrecedesGreetingAndResetKeepsAuthenticationState() {
    await using var stream = new SessionDuplexStream("OPTION flag=1\nRESET\nNOP\nBYE\n"u8.ToArray());
    var hooks = new Hooks();
    var state = new Counter();
    var session = new ServerSession<Counter>(new AcceptedConnection(stream), state,
      new CommandRegistry<Counter>(), hooks, ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.True(hooks._authSawUnauthenticated);
    Assert.True(hooks._resetSawAuthenticated);
    Assert.Equal(SessionEnd.Clean, hooks._endReason);
    Assert.Equal(0, state._value);
    Assert.Equal("S AUTH yes\nOK\nOK\nOK\nOK\nOK\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task AbandonedInquiryTerminatesSessionWithoutInventingAFinal() {
    await using var stream = new SessionDuplexStream("LEAK\nD payload\nEND\n"u8.ToArray());
    var registry = new CommandRegistry<Counter>();
    registry.Register(new Handler("LEAK", async (context, token) => {
      await using var inquiry = await context.BeginInquiryAsync("PIN", ReadOnlyMemory<byte>.Empty,
        PayloadSensitivity.Secret, token);
    }));
    var session = new ServerSession<Counter>(new AcceptedConnection(stream), new Counter(), registry,
      new DefaultSessionHooks<Counter>(), ServerOptions.Default);
    var error = await Assert.ThrowsAsync<ServerException>(() => session.RunAsync(TestContext.Current.CancellationToken));
    Assert.Equal(ServerErrorKind.Protocol, error.Kind);
    Assert.Equal("OK\nINQUIRE PIN\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task MalformedAndUnknownOptionsHaveDistinctRecoverableCodes() {
    await using var stream = new SessionDuplexStream("OPTION\nOPTION flag\nNOP\nBYE\n"u8.ToArray());
    var session = new ServerSession<Counter>(new AcceptedConnection(stream), new Counter(),
      new CommandRegistry<Counter>(), new DefaultSessionHooks<Counter>(), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal("OK\nERR 55 invalid value\nERR 174 unknown option\nOK\nOK\n"u8.ToArray(),
      stream.WrittenBytes);
  }

  [Fact]
  public async Task RetainedArgumentMemoryIsClearedAfterTheHandlerInvocation() {
    await using var stream = new SessionDuplexStream("ECHO private\nBYE\n"u8.ToArray());
    ReadOnlyMemory<byte> borrowed = default;
    var registry = new CommandRegistry<Counter>();
    registry.Register(new Handler("ECHO", (context, _) => {
      borrowed = context.Arguments;
      return ValueTask.CompletedTask;
    }));
    var session = new ServerSession<Counter>(new AcceptedConnection(stream), new Counter(), registry,
      new DefaultSessionHooks<Counter>(), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal(7, borrowed.Length);
    Assert.Equal(new byte[7], borrowed.ToArray());
  }

  private sealed class Counter {
    internal int _value;
  }

  private sealed class Handler(string name, Func<CommandContext<Counter>, CancellationToken, ValueTask> call)
    : ICommandHandler<Counter> {
    public string Name => name;
    public string Description => "counter";

    public ValueTask HandleAsync(CommandContext<Counter> context, CancellationToken cancellationToken)
      => call(context, cancellationToken);
  }

  private sealed class Hooks : ISessionHooks<Counter> {
    internal bool _authSawUnauthenticated;
    internal SessionEnd? _endReason;
    internal bool _resetSawAuthenticated;

    public async ValueTask AuthenticateAsync(CommandContext<Counter> context, CancellationToken token) {
      _authSawUnauthenticated = !context.IsAuthenticated;
      await context.SendStatusAsync("AUTH", "yes"u8.ToArray(), token);
    }

    public ValueTask OptionAsync(OptionRequest request, CommandContext<Counter> context, CancellationToken token) {
      context.State._value = request.Value!.Value.Length;
      return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync(CommandContext<Counter> context, CancellationToken token) {
      _resetSawAuthenticated = context.IsAuthenticated;
      context.State._value = 0;
      return ValueTask.CompletedTask;
    }

    public ValueTask ClosedAsync(Counter state, SessionEnd reason, CancellationToken token) {
      _endReason = reason;
      return ValueTask.CompletedTask;
    }
  }
}
