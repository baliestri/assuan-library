// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server;
using AssuanLibrary.Server.Sessions;
using AssuanLibrary.Tests.Client.Fakes;
using AssuanLibrary.Tests.Server.Fakes;

namespace AssuanLibrary.Tests.Server.Sessions;

public sealed class AssuanSessionContextTests {
  [Fact]
  public void Constructor_ShouldThrowArgumentNullException_WhenConnectionIsNull()
    => Should.Throw<ArgumentNullException>(() => new AssuanSessionContext(null!));

  [Fact]
  public void Constructor_ShouldExposeConnection_SessionAndServerContext() {
    var connection = new FakeAssuanConnection();

    using var context = new AssuanSessionContext(connection);

    context.Connection.ShouldBeSameAs(connection);
    context.Session.ShouldNotBeNull();
    context.ServerContext.ShouldNotBeNull();
    context.ServerContext.Session.ShouldBeSameAs(context.Session);
  }

  [Fact]
  public void Constructor_ShouldForwardCancellationToken_ToSession() {
    var connection = new FakeAssuanConnection();
    using var cts = new CancellationTokenSource();

    using var context = new AssuanSessionContext(connection, cts.Token);

    context.Session.CancellationToken.IsCancellationRequested.ShouldBeFalse();
    cts.Cancel();
    context.Session.CancellationToken.IsCancellationRequested.ShouldBeTrue();
  }

  [Fact]
  public async Task CreateSessionLoop_ShouldReturnWorkingLoop_WiredToProvidedDispatcher() {
    var connection = new FakeAssuanConnection();
    using var cts = new CancellationTokenSource();
    using var context = new AssuanSessionContext(connection, cts.Token);

    var dispatcher = new FakeCommandDispatcher();
    var loop = context.CreateSessionLoop(dispatcher);

    loop.ShouldNotBeNull();

    connection.ReadBuffers.Enqueue("GETINFO version\n"u8.ToArray());

    var runTask = Task.Run(() => loop.Run());

    var dispatched = SpinWait.SpinUntil(() => dispatcher.DispatchCalls.Count > 0, TimeSpan.FromSeconds(2));
    cts.Cancel();
    await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(2)));

    dispatched.ShouldBeTrue();
    dispatcher.DispatchCalls.Count.ShouldBe(1);
    dispatcher.DispatchCalls[0].context.ShouldBeSameAs(context.ServerContext);
  }

  [Fact]
  public void Dispose_ShouldDisposeConnection() {
    var connection = new FakeAssuanConnection();
    var context = new AssuanSessionContext(connection);

    context.Dispose();

    connection.DisposeCalls.ShouldBe(1);
  }

  [Fact]
  public void Dispose_ShouldBeSafe_WhenCalledMultipleTimes() {
    var connection = new FakeAssuanConnection();
    var context = new AssuanSessionContext(connection);

    context.Dispose();
    Should.NotThrow(() => context.Dispose());
  }

  [Fact]
  public async Task DisposeAsync_ShouldDisposeConnectionAsync() {
    var connection = new FakeAssuanConnection();
    var context = new AssuanSessionContext(connection);

    await context.DisposeAsync();

    connection.DisposeAsyncCalls.ShouldBe(1);
  }
}
