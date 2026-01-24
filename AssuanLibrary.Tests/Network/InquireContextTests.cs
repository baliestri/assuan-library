// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Network;
using JetBrains.Annotations;
using Moq;

namespace AssuanLibrary.Tests.Network;

[TestSubject(typeof(IInquireContext))]
public sealed class InquireContextTests {
  [Test]
  public void Keyword_ShouldBeReadable() {
    var mock = new Mock<IInquireContext>();

    mock.SetupGet(c => c.Keyword).Returns("KEYWORD");

    mock.Object.Keyword.ShouldBe("KEYWORD");
  }

  [Test]
  public void Parameters_ShouldBeReadable() {
    var parameters = new[] { "a", "b" };
    var mock = new Mock<IInquireContext>();

    mock.SetupGet(c => c.Parameters).Returns(parameters);

    mock.Object.Parameters.ShouldBe(parameters);
  }

  [Test]
  public void Write_String_ShouldBeCallable() {
    var mock = new Mock<IInquireContext>(MockBehavior.Strict);

    mock.Setup(c => c.Write("value"));

    mock.Object.Write("value");

    mock.Verify(c => c.Write("value"), Times.Once);
  }

  [Test]
  public void Write_Buffer_ShouldBeCallable() {
    var buffer = new byte[] { 1, 2, 3 };
    var mock = new Mock<IInquireContext>(MockBehavior.Strict);

    mock.Setup(c => c.Write(buffer));

    mock.Object.Write(buffer);

    mock.Verify(c => c.Write(buffer), Times.Once);
  }

  [Test]
  public void End_ShouldBeCallable() {
    var mock = new Mock<IInquireContext>(MockBehavior.Strict);

    mock.Setup(c => c.End());

    mock.Object.End();

    mock.Verify(c => c.End(), Times.Once);
  }

  [Test]
  public void Cancel_ShouldBeCallable() {
    var mock = new Mock<IInquireContext>(MockBehavior.Strict);

    mock.Setup(c => c.Cancel());

    mock.Object.Cancel();

    mock.Verify(c => c.Cancel(), Times.Once);
  }

  [Test]
  public async Task WriteAsync_String_ShouldBeAwaitable() {
    var mock = new Mock<IInquireContext>();

    mock.Setup(c => c.WriteAsync("value", It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    await mock.Object.WriteAsync("value");

    mock.Verify(c => c.WriteAsync("value", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Test]
  public async Task WriteAsync_Buffer_ShouldBeAwaitable() {
    var buffer = new byte[] { 9 };
    var mock = new Mock<IInquireContext>();

    mock.Setup(c => c.WriteAsync(buffer, It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    await mock.Object.WriteAsync(buffer);

    mock.Verify(c => c.WriteAsync(buffer, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Test]
  public async Task EndAsync_ShouldBeAwaitable() {
    var mock = new Mock<IInquireContext>();

    mock.Setup(c => c.EndAsync(It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    await mock.Object.EndAsync();

    mock.Verify(
      c => c.EndAsync(It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Test]
  public async Task CancelAsync_ShouldBeAwaitable() {
    var mock = new Mock<IInquireContext>();

    mock.Setup(c => c.CancelAsync(It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    await mock.Object.CancelAsync();

    mock.Verify(
      c => c.CancelAsync(It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Test]
  public async Task AsyncMethods_ShouldReceiveCancellationToken() {
    var cts = new CancellationTokenSource();
    var receivedToken = CancellationToken.None;

    var mock = new Mock<IInquireContext>();

    mock.Setup(c => c.WriteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Callback<string, CancellationToken>((_, ct) => {
        receivedToken = ct;
      })
      .Returns(ValueTask.CompletedTask);

    await mock.Object.WriteAsync("value", cts.Token);

    receivedToken.ShouldBe(cts.Token);
  }
}
