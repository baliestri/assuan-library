// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server;
using AssuanLibrary.Tests.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests;

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
    Assert.Equal(1, hooks.Authentications);
    Assert.Equal(1, hooks.UnknownOptions);
    Assert.Equal(1, hooks.Resets);
    Assert.Equal(1, hooks.Closures);
    Assert.Equal(1, state.OptionCount);
    Assert.Equal("on"u8.ToArray(), state.LastValue);
    Assert.Equal("OK\nOK\nOK\nOK\nOK\n"u8.ToArray(), peer.WrittenBytes);
  }

  private sealed class RecordingHooks : ISessionHooks<GeneratedSessionState> {
    internal int Authentications, UnknownOptions, Resets, Closures;

    public ValueTask AuthenticateAsync(CommandContext<GeneratedSessionState> context, CancellationToken token) {
      Authentications++;
      return ValueTask.CompletedTask;
    }

    public ValueTask OptionAsync(OptionRequest request, CommandContext<GeneratedSessionState> context,
      CancellationToken token) {
      UnknownOptions++;
      return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync(CommandContext<GeneratedSessionState> context, CancellationToken token) {
      Resets++;
      return ValueTask.CompletedTask;
    }

    public ValueTask ClosedAsync(GeneratedSessionState state, SessionEnd reason, CancellationToken token) {
      Closures++;
      return ValueTask.CompletedTask;
    }
  }
}
