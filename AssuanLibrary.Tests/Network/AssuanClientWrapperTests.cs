// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Network;
using JetBrains.Annotations;
using Moq;

namespace AssuanLibrary.Tests.Network;

[TestSubject(typeof(IAssuanClientWrapper))]
public sealed class AssuanClientWrapperTests {
  [Test]
  public void IsConnected_ShouldBeConfigurable() {
    var mock = new Mock<IAssuanClientWrapper>();

    mock.SetupGet(c => c.IsConnected).Returns(true);

    mock.Object.IsConnected.ShouldBeTrue();
  }

  [Test]
  public void Connect_ShouldBeCallable() {
    var mock = new Mock<IAssuanClientWrapper>(MockBehavior.Strict);

    mock.Setup(c => c.Connect());

    mock.Object.Connect();

    mock.Verify(c => c.Connect(), Times.Once);
  }

  [Test]
  public void Disconnect_ShouldBeCallable() {
    var mock = new Mock<IAssuanClientWrapper>(MockBehavior.Strict);

    mock.Setup(c => c.Disconnect());

    mock.Object.Disconnect();

    mock.Verify(c => c.Disconnect(), Times.Once);
  }

  [Test]
  public void Write_ShouldBeCallable() {
    var buffer = new byte[] { 1, 2, 3 };
    var mock = new Mock<IAssuanClientWrapper>(MockBehavior.Strict);

    mock.Setup(c => c.Write(buffer));

    mock.Object.Write(buffer);

    mock.Verify(c => c.Write(buffer), Times.Once);
  }

  [Test]
  public void Read_ShouldReturnBuffer() {
    var expected = new byte[] { 4, 5 };
    var mock = new Mock<IAssuanClientWrapper>();

    mock.Setup(c => c.Read()).Returns(expected);

    var result = mock.Object.Read();

    result.ShouldBeSameAs(expected);
  }

  [Test]
  public void Read_WithInquireHandler_ShouldInvokeHandler() {
    var handlerCalled = false;
    var mock = new Mock<IAssuanClientWrapper>();

    mock
      .Setup(c => c.Read(It.IsAny<Action<IInquireContext>>()))
      .Callback<Action<IInquireContext>>(handler => {
        handler(Mock.Of<IInquireContext>());
        handlerCalled = true;
      })
      .Returns(Array.Empty<byte>());

    mock.Object.Read(_ => { });

    handlerCalled.ShouldBeTrue();
  }

  [Test]
  public async Task ConnectAsync_ShouldBeAwaitable() {
    var mock = new Mock<IAssuanClientWrapper>();

    mock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await mock.Object.ConnectAsync();

    mock.Verify(c => c.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Test]
  public async Task DisconnectAsync_ShouldBeAwaitable() {
    var mock = new Mock<IAssuanClientWrapper>();

    mock.Setup(c => c.DisconnectAsync(It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await mock.Object.DisconnectAsync();

    mock.Verify(c => c.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
  }

  [Test]
  public async Task WriteAsync_ShouldBeAwaitable() {
    var buffer = new byte[] { 9 };
    var mock = new Mock<IAssuanClientWrapper>();

    mock.Setup(c => c.WriteAsync(buffer, It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    await mock.Object.WriteAsync(buffer);

    mock.Verify(c => c.WriteAsync(buffer, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Test]
  public async Task ReadAsync_ShouldReturnBuffer() {
    var expected = new byte[] { 7, 8 };
    var mock = new Mock<IAssuanClientWrapper>();

    mock.Setup(c => c.ReadAsync(It.IsAny<CancellationToken>()))
      .Returns(new ValueTask<byte[]>(expected));

    var result = await mock.Object.ReadAsync();

    result.ShouldBeSameAs(expected);
  }

  [Test]
  public async Task ReadAsync_WithInquireHandler_ShouldInvokeHandler() {
    var handlerCalled = false;
    var mock = new Mock<IAssuanClientWrapper>();

    mock
      .Setup(c => c.ReadAsync(
        It.IsAny<Func<IInquireContext, CancellationToken, Task>>(),
        It.IsAny<CancellationToken>()))
      .Callback<Func<IInquireContext, CancellationToken, Task>, CancellationToken>((handler, ct) => {
        handler(Mock.Of<IInquireContext>(), ct).GetAwaiter().GetResult();
        handlerCalled = true;
      })
      .Returns(new ValueTask<byte[]>(Array.Empty<byte>()));

    await mock.Object.ReadAsync((_, _) => Task.CompletedTask);

    handlerCalled.ShouldBeTrue();
  }

  [Test]
  public void Dispose_ShouldBeCallable() {
    var mock = new Mock<IAssuanClientWrapper>(MockBehavior.Strict);

    mock.Setup(c => c.Dispose());

    mock.Object.Dispose();

    mock.Verify(c => c.Dispose(), Times.Once);
  }

  [Test]
  public async Task DisposeAsync_ShouldBeCallable() {
    var mock = new Mock<IAssuanClientWrapper>();

    mock.Setup(c => c.DisposeAsync())
      .Returns(ValueTask.CompletedTask);

    await mock.Object.DisposeAsync();

    mock.Verify(c => c.DisposeAsync(), Times.Once);
  }
}
