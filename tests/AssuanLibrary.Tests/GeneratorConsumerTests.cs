// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Attributes;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests;

public sealed class GeneratedSessionState {
  public byte[]? LastValue;
  public int OptionCount;
}

[AssuanHandlerGroup]
public partial class GeneratedDemo {
  [AssuanCommandHandler("ECHO", "Echoes arguments")]
  private static ValueTask EchoAsync(CommandContext<GeneratedSessionState> context, CancellationToken token)
    => context.SendDataAsync(context.Arguments, token);

  [AssuanOptionHandler("mode")]
  private static ValueTask ModeAsync(OptionRequest request, CommandContext<GeneratedSessionState> context,
  CancellationToken token) {
    context.State.OptionCount++;
    context.State.LastValue = request.Value?.ToArray();
    return ValueTask.CompletedTask;
  }
}

public sealed class GeneratorConsumerTests {
  [Fact]
  public void AnalyzerReferenceGeneratesMembersUsableWithoutReflection() {
    var registry = new CommandRegistry<GeneratedSessionState>();
    GeneratedDemo.RegisterHandlers(registry);
    Assert.NotNull(registry.Find("ECHO"));
    var server = new AssuanServer<GeneratedSessionState>(() => new GeneratedSessionState(), ServerOptions.Default);
    GeneratedDemo.RegisterHandlers(server);
    Assert.Throws<ArgumentNullException>(() => GeneratedDemo.CreateHooks(null!));
  }

  [Fact]
  public async Task GeneratedOptionDispatcherPreservesAuthenticationAndOtherHooks() {
    var hooks = new RecordingHooks();
    var state = new GeneratedSessionState();
    var registry = new CommandRegistry<GeneratedSessionState>();
    GeneratedDemo.RegisterHandlers(registry);
    await using var peer = new SessionDuplexStream("OPTION mode=on\nOPTION other\nRESET\nBYE\n"u8.ToArray());
    var session = new ServerSession<GeneratedSessionState>(new AcceptedConnection(peer), state,
      registry, GeneratedDemo.CreateHooks(hooks), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal(1, hooks._authentications);
    Assert.Equal(1, hooks._unknownOptions);
    Assert.Equal(1, hooks._resets);
    Assert.Equal(1, hooks._closures);
    Assert.Equal(1, state.OptionCount);
    Assert.Equal("on"u8.ToArray(), state.LastValue);
    Assert.Equal("OK\nOK\nOK\nOK\nOK\n"u8.ToArray(), peer.WrittenBytes);
  }

  private sealed class RecordingHooks : ISessionHooks<GeneratedSessionState> {
    internal int _authentications, _unknownOptions, _resets, _closures;

    public ValueTask AuthenticateAsync(CommandContext<GeneratedSessionState> context, CancellationToken token) {
      _authentications++;
      return ValueTask.CompletedTask;
    }

    public ValueTask OptionAsync(OptionRequest request, CommandContext<GeneratedSessionState> context,
    CancellationToken token) {
      _unknownOptions++;
      return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync(CommandContext<GeneratedSessionState> context, CancellationToken token) {
      _resets++;
      return ValueTask.CompletedTask;
    }

    public ValueTask ClosedAsync(GeneratedSessionState state, SessionEnd reason, CancellationToken token) {
      _closures++;
      return ValueTask.CompletedTask;
    }
  }
}
