// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Server.Dispatching;
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
    cts.CancelAfter(20);

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

  [Fact]
  public async Task RunAsync_ShouldRespectMaxConcurrentSessions() {
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, 0);
    var listener = new QueuedAsyncListener(endpoint, [new FakeAssuanConnection(), new FakeAssuanConnection(), new FakeAssuanConnection()]);
    var factory = new SingleListenerFactory(listener);
    var dispatcher = new FakeCommandDispatcher();

    var authenticationCalls = 0;
    var activeSessions = 0;
    var observedMaxConcurrency = 0;
    var allAuthenticated = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

    var options = new AssuanServerOptions {
      Banner = null,
      MaxConcurrentSessions = 2,
      ContinueOnSessionError = true,
      OnAuthenticateSessionAsync = async context => {
        var currentActiveSessions = Interlocked.Increment(ref activeSessions);
        UpdateMax(ref observedMaxConcurrency, currentActiveSessions);

        var callCount = Interlocked.Increment(ref authenticationCalls);
        await Task.Delay(50).ConfigureAwait(false);

        context.Session.CloseGracefully();
        Interlocked.Decrement(ref activeSessions);

        if (callCount == 3) {
          allAuthenticated.TrySetResult(null);
        }
      }
    };

    var server = new AssuanServer(factory, dispatcher, options);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var runTask = server.RunAsync(endpoint, cts.Token);

    await allAuthenticated.Task;
    cts.Cancel();
    await runTask;

    authenticationCalls.ShouldBe(3);
    observedMaxConcurrency.ShouldBeLessThanOrEqualTo(2);
  }

  [Fact]
  public async Task RunAsync_ShouldContinueAfterSessionError_WhenConfigured() {
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, 0);

    var firstConnection = new FakeAssuanConnection();
    firstConnection.ReadBuffers.Enqueue("PING\n"u8.ToArray());

    var secondConnection = new FakeAssuanConnection();
    secondConnection.ReadBuffers.Enqueue("PING\n"u8.ToArray());

    var listener = new QueuedAsyncListener(endpoint, [firstConnection, secondConnection]);
    var factory = new SingleListenerFactory(listener);

    var secondSessionProcessed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
    var dispatcher = new FailFirstSessionDispatcher(secondSessionProcessed);

    var options = new AssuanServerOptions {
      Banner = null,
      MaxConcurrentSessions = 2,
      ContinueOnSessionError = true
    };

    var server = new AssuanServer(factory, dispatcher, options);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    var runTask = server.RunAsync(endpoint, cts.Token);

    await secondSessionProcessed.Task;
    cts.Cancel();
    await runTask;

    dispatcher.DispatchCalls.ShouldBeGreaterThanOrEqualTo(2);
  }

  [Fact]
  public async Task RunAsync_ShouldStopOnSessionError_WhenConfigured() {
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, 0);

    var connection = new FakeAssuanConnection();
    connection.ReadBuffers.Enqueue("PING\n"u8.ToArray());

    var listener = new QueuedAsyncListener(endpoint, [connection]);
    var factory = new SingleListenerFactory(listener);

    var dispatcher = new AlwaysThrowDispatcher();
    var options = new AssuanServerOptions {
      Banner = null,
      MaxConcurrentSessions = 1,
      ContinueOnSessionError = false
    };

    var server = new AssuanServer(factory, dispatcher, options);

    await Should.ThrowAsync<InvalidOperationException>(async () => await server.RunAsync(endpoint, CancellationToken.None));
  }

  [Fact]
  public async Task RunAsync_ShouldThrow_WhenMaxConcurrentSessionsIsInvalid() {
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, 0);
    var listener = new QueuedAsyncListener(endpoint, []);
    var factory = new SingleListenerFactory(listener);

    var options = new AssuanServerOptions {
      Banner = null,
      MaxConcurrentSessions = 0
    };

    var server = new AssuanServer(factory, new FakeCommandDispatcher(), options);

    await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await server.RunAsync(endpoint, CancellationToken.None));
  }

  [Fact]
  public async Task RunAsync_ShouldThrow_WhenCalledConcurrently() {
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, 0);
    var listener = new QueuedAsyncListener(endpoint, []);
    var factory = new SingleListenerFactory(listener);
    var dispatcher = new FakeCommandDispatcher();

    var options = new AssuanServerOptions {
      Banner = null,
      MaxConcurrentSessions = 1
    };

    var server = new AssuanServer(factory, dispatcher, options);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    var firstRunTask = server.RunAsync(endpoint, cts.Token);
    await Task.Delay(25);

    await Should.ThrowAsync<InvalidOperationException>(async () => await server.RunAsync(endpoint, CancellationToken.None));

    cts.Cancel();
    await firstRunTask;
  }

  private static void UpdateMax(ref int target, int candidate) {
    while (true) {
      var snapshot = Volatile.Read(ref target);
      if (candidate <= snapshot) {
        return;
      }

      if (Interlocked.CompareExchange(ref target, candidate, snapshot) == snapshot) {
        return;
      }
    }
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

  private sealed class QueuedAsyncListener(IAssuanEndpoint endpoint, IReadOnlyCollection<IAssuanConnection> connections) : IAssuanListener {
    private readonly Queue<IAssuanConnection> _acceptQueue = new(connections);

    public IAssuanEndpoint Endpoint { get; } = endpoint;

    public IAssuanConnection Accept() {
      if (_acceptQueue.Count == 0) {
        throw new InvalidOperationException("No connection enqueued.");
      }

      return _acceptQueue.Dequeue();
    }

    public async ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
      if (_acceptQueue.Count > 0) {
        return _acceptQueue.Dequeue();
      }

      await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
      throw new OperationCanceledException(ct);
    }
  }

  private sealed class FailFirstSessionDispatcher(TaskCompletionSource<object?> secondSessionProcessed) : ICommandDispatcher {
    public int DispatchCalls { get; private set; }

    public bool TryAdd(CommandHandler handler) => true;

    public void Dispatch(IReadOnlyAssuanCommand command, IServerContext context)
      => throw new NotSupportedException("Async server path should use DispatchAsync.");

    public Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context) {
      DispatchCalls++;

      if (DispatchCalls == 1) {
        throw new InvalidOperationException("First session failed.");
      }

      context.Session.CloseGracefully();
      secondSessionProcessed.TrySetResult(null);
      return Task.CompletedTask;
    }
  }

  private sealed class AlwaysThrowDispatcher : ICommandDispatcher {
    public bool TryAdd(CommandHandler handler) => true;

    public void Dispatch(IReadOnlyAssuanCommand command, IServerContext context)
      => throw new NotSupportedException("Async server path should use DispatchAsync.");

    public Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context)
      => throw new InvalidOperationException("Session failed.");
  }
}
