// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Server.Dispatching;
using AssuanLibrary.Server.Sessions;
using AssuanLibrary.Tests.Client.Fakes;

namespace AssuanLibrary.Tests.Server;

public sealed class AssuanSessionRunnerTests {
  [Fact]
  public async Task RunAsync_ShouldAuthenticate_SendBanner_AndDisposeConnection() {
    var dispatcher = new PassThroughDispatcher();
    var conn = new FakeAssuanConnection();

    var authenticated = false;
    var options = new AssuanServerOptions {
      Banner = "runner-ready",
      OnAuthenticateSessionAsync = context => {
        authenticated = true;
        context.Session.CloseGracefully();
        return Task.CompletedTask;
      }
    };

    var runner = new AssuanSessionRunner(dispatcher, options);

    await runner.RunAsync(conn, CancellationToken.None);

    authenticated.ShouldBeTrue();
    conn.Writes.Count.ShouldBe(1);
    new AssuanResponse(conn.Writes[0]).Type.ShouldBe(AssuanResponseType.Ok);
    conn.DisposeAsyncCalls.ShouldBe(1);
  }

  [Fact]
  public void Run_ShouldCaptureSessionException_AndDisposeConnection() {
    var dispatcher = new ThrowingDispatcher();
    var conn = new FakeAssuanConnection();
    conn.ReadBuffers.Enqueue("PING\n"u8.ToArray());

    var runner = new AssuanSessionRunner(dispatcher, new AssuanServerOptions { Banner = null });

    Should.Throw<InvalidOperationException>(() => runner.Run(conn));
    conn.DisposeCalls.ShouldBe(1);
  }

  private sealed class PassThroughDispatcher : ICommandDispatcher {
    public bool TryAdd(CommandHandler handler) => true;

    public void Dispatch(IReadOnlyAssuanCommand command, IServerContext context) => context.Session.CloseGracefully();

    public Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context) {
      context.Session.CloseGracefully();
      return Task.CompletedTask;
    }
  }

  private sealed class ThrowingDispatcher : ICommandDispatcher {
    public bool TryAdd(CommandHandler handler) => true;

    public void Dispatch(IReadOnlyAssuanCommand command, IServerContext context)
      => throw new InvalidOperationException("session failure");

    public Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context)
      => Task.FromException(new InvalidOperationException("session failure"));
  }
}
