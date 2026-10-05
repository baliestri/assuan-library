// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;

namespace AssuanLibrary.Tests.Client;

public sealed class GreetingTests {
  [Fact]
  public async Task CommentsStatusAndEmptyLinesPrecedeTheFirstOkWithoutSendingACommand() {
    var stream = new ScriptedClientStream("# note\nS READY now\n\nOK ready\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(client.IsUsable);
    Assert.Empty(stream.WrittenBytes);
  }

  [Fact]
  public async Task GreetingErrorKeepsCompleteUnsignedCodeAndDisposesStream() {
    var stream = new ScriptedClientStream("ERR 4294967295 denied\n"u8.ToArray());
    var error = await Assert.ThrowsAsync<ClientException>(() => AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.GreetingRejected, error.Kind);
    Assert.Equal(uint.MaxValue, error.RemoteCode);
    Assert.DoesNotContain("denied", error.ToString());
    Assert.True(stream.Closed);
  }

  [Fact]
  public async Task InquiryBeforeGreetingOkIsCancelledAndFinalStillRequired() {
    var stream = new ScriptedClientStream("INQUIRE PIN\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(client.IsUsable);
    Assert.Equal("CAN\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task InvalidOptionsAndCancelledReadDisposeOwnedStream() {
    Assert.Equal(ClientErrorKind.InvalidOptions,
      Assert.Throws<ClientException>(() => new ClientOptions(connectTimeout: TimeSpan.Zero)).Kind);
    Assert.Equal(ClientErrorKind.InvalidOptions,
      Assert.Throws<ClientException>(() => new ClientOptions(commandTimeout: TimeSpan.MaxValue)).Kind);
    Assert.Equal(ClientErrorKind.InvalidOptions,
      Assert.Throws<ClientException>(() => new ClientOptions(greetingTimeout: TimeSpan.Zero)).Kind);

    using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    await cancelled.CancelAsync();
    var stream = new ScriptedClientStream("OK\n"u8.ToArray());
    var error = await Assert.ThrowsAsync<ClientException>(() => AssuanClient.FromStreamAsync(stream,
      cancellationToken: cancelled.Token));
    Assert.Equal(ClientErrorKind.Cancelled, error.Kind);
    Assert.True(stream.Closed);
  }
}
