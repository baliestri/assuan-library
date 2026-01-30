// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Tests.Client.Fakes;
using AssuanLibrary.Tests.Server.Fakes;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Tests.Server;

public sealed class AssuanServerTests {
  [Fact]
  public void RegisterCommandHandler_ShouldThrow_WhenDuplicate() {
    var dispatcher = new FakeCommandDispatcher { TryAddResult = false };
    var factory = new FakeAssuanListenerFactory();

    var server = new AssuanServer(factory, dispatcher, AssuanServerOptions.Default);

    Should.Throw<InvalidOperationException>(() => server.RegisterCommandHandler(new TestCommandHandler()));
  }

  [Fact]
  public void RegisterCommandHandler_Generic_ShouldThrow_WhenDuplicate() {
    var dispatcher = new FakeCommandDispatcher { TryAddResult = false };
    var factory = new FakeAssuanListenerFactory();

    var server = new AssuanServer(factory, dispatcher, AssuanServerOptions.Default);

    Should.Throw<InvalidOperationException>(() => server.RegisterCommandHandler<TestCommandHandler>());
  }

  [Fact]
  public async Task RunAsync_ShouldAuthenticate_SendBanner_AndDispose() {
    var dispatcher = new FakeCommandDispatcher();

    // Listener factory that always returns the same listener instance so we can enqueue before starting.
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, 0);
    var sharedListener = new FakeAssuanListener(endpoint);
    var factory = new SingleListenerFactory(sharedListener);

    var conn = new FakeAssuanConnection();
    sharedListener.AcceptQueue.Enqueue(conn);

    using var cts = new CancellationTokenSource();

    // stop the server after the first session completes by cancelling after a tiny delay
    // (session loop will honor cancellation token).
    _ = Task.Run(async () => {
      await Task.Delay(20);
      cts.Cancel();
    });

    var calls = new List<string>();
    var options = new AssuanServerOptions {
      Banner = "hello",
      OnAuthenticateSessionAsync = ctx => {
        calls.Add("auth");
        ctx.Session.ShouldNotBeNull();
        return Task.CompletedTask;
      }
    };

    // Ensure the session loop exits: internal reads should just return empty buffers.
    // (FakeAssuanConnection.InternalRead delegates to Read(), which returns empty by default.)

    var server = new AssuanServer(factory, dispatcher, options);

    await server.RunAsync(endpoint, cts.Token);

    calls.ShouldContain("auth");

    conn.Writes.Count.ShouldBe(1);
    var bannerResponse = new AssuanResponse(conn.Writes[0]);
    bannerResponse.Type.ShouldBe(AssuanResponseType.Ok);

    conn.DisposeAsyncCalls.ShouldBe(1);
  }

  private sealed class TestCommandHandler : CommandHandler {
    public override string Name => "ECHO";

    public override void Handle(IReadOnlyAssuanCommand command, IServerContext serverContext)
      => serverContext.SendResponse(AssuanResponse.Ok(command.Count > 1 ? command[1] : string.Empty));
  }

  private sealed class SingleListenerFactory : IAssuanListenerFactory {
    private readonly IAssuanListener _listener;

    public SingleListenerFactory(IAssuanListener listener) => _listener = listener;

    public IAssuanListener CreateListener(IAssuanEndpoint endpoint) => _listener;
  }
}
