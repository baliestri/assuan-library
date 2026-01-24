// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Network;
using JetBrains.Annotations;
using Moq;

namespace AssuanLibrary.Tests.Network;

[TestSubject(typeof(IAssuanClient))]
public sealed class AssuanClientTests {
  [Test]
  public void IsConnected_ShouldBeConfigurable() {
    var mock = new Mock<IAssuanClient>();

    mock.SetupGet(c => c.IsConnected).Returns(true);

    mock.Object.IsConnected.ShouldBeTrue();
  }

  [Test]
  public void Connect_ShouldBeCallable() {
    var mock = new Mock<IAssuanClient>(MockBehavior.Strict);

    mock.Setup(c => c.Connect());

    mock.Object.Connect();

    mock.Verify(c => c.Connect(), Times.Once);
  }

  [Test]
  public void Disconnect_ShouldBeCallable() {
    var mock = new Mock<IAssuanClient>(MockBehavior.Strict);

    mock.Setup(c => c.Disconnect());

    mock.Object.Disconnect();

    mock.Verify(c => c.Disconnect(), Times.Once);
  }

  [Test]
  public async Task ConnectAsync_ShouldBeAwaitable() {
    var mock = new Mock<IAssuanClient>();

    mock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await mock.Object.ConnectAsync();

    mock.Verify(c => c.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Test]
  public async Task DisconnectAsync_ShouldBeAwaitable() {
    var mock = new Mock<IAssuanClient>();

    mock.Setup(c => c.DisconnectAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await mock.Object.DisconnectAsync();

    mock.Verify(c => c.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Test]
  public void Invoke_ShouldReturnResponseCollection() {
    var command = new AssuanCommand("TEST");
    var response = new AssuanResponseCollection();

    var mock = new Mock<IAssuanClient>();

    mock.Setup(c => c.Invoke(command)).Returns(response);

    var result = mock.Object.Invoke(command);

    result.ShouldBeSameAs(response);
  }

  [Test]
  public void Invoke_WithInquireHandler_ShouldInvokeHandler() {
    var command = new AssuanCommand("TEST");
    var handlerCalled = false;

    var mock = new Mock<IAssuanClient>();

    mock.Setup(c => c.Invoke(
        command,
        It.IsAny<Action<IInquireContext>>()))
      .Callback<AssuanCommand, Action<IInquireContext>>((_, handler) => {
        handler(Mock.Of<IInquireContext>());
        handlerCalled = true;
      })
      .Returns(new AssuanResponseCollection());

    mock.Object.Invoke(command, _ => { });

    handlerCalled.ShouldBeTrue();
  }

  [Test]
  public async Task InvokeAsync_ShouldReturnResponseCollection() {
    var command = new AssuanCommand("ASYNC");
    var response = new AssuanResponseCollection();

    var mock = new Mock<IAssuanClient>();

    mock.Setup(c => c.InvokeAsync(command, It.IsAny<CancellationToken>()))
      .Returns(new ValueTask<AssuanResponseCollection>(response));

    var result = await mock.Object.InvokeAsync(command);

    result.ShouldBeSameAs(response);
  }

  [Test]
  public async Task InvokeAsync_WithInquireHandler_ShouldInvokeHandler() {
    var command = new AssuanCommand("INQUIRE");
    var handlerCalled = false;

    var mock = new Mock<IAssuanClient>();

    mock.Setup(c => c.InvokeAsync(
        command,
        It.IsAny<Func<IInquireContext, CancellationToken, Task>>(),
        It.IsAny<CancellationToken>()))
      .Callback<AssuanCommand,
        Func<IInquireContext, CancellationToken, Task>,
        CancellationToken>((_, handler, ct) => {
        handler(Mock.Of<IInquireContext>(), ct).GetAwaiter().GetResult();
        handlerCalled = true;
      })
      .Returns(new ValueTask<AssuanResponseCollection>(
        new AssuanResponseCollection()));

    await mock.Object.InvokeAsync(
      command,
      (_, _) => Task.CompletedTask,
      CancellationToken.None);

    handlerCalled.ShouldBeTrue();
  }

  [Test]
  public void Dispose_ShouldBeCallable() {
    var mock = new Mock<IAssuanClient>(MockBehavior.Strict);

    mock.Setup(c => c.Dispose());

    mock.Object.Dispose();

    mock.Verify(c => c.Dispose(), Times.Once);
  }

  [Test]
  public async Task DisposeAsync_ShouldBeCallable() {
    var mock = new Mock<IAssuanClient>();

    mock.Setup(c => c.DisposeAsync())
      .Returns(ValueTask.CompletedTask);

    await mock.Object.DisposeAsync();

    mock.Verify(c => c.DisposeAsync(), Times.Once);
  }
}
