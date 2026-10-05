// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;

namespace AssuanLibrary.Tests.Client;

public sealed class InquiryCollectionTests {
  [Fact]
  public async Task CollectionAutomaticallyCancelsInquiryAndWaitsForRemoteErr() {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray(),
      "INQUIRE PIN\nERR 7 cancelled\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<ClientException>(() => client.CollectAsync("ASK",
      ReadOnlyMemory<byte>.Empty, cancellationToken: TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Remote, error.Kind);
    Assert.Equal((uint)7, error.RemoteCode);
    Assert.Equal("ASK\nCAN\n"u8.ToArray(), peer.WrittenBytes);
    Assert.True(client.IsUsable);
  }

  [Theory]
  [InlineData(1, false)]
  [InlineData(2, true)]
  public async Task InquiryAndFinalBothCountTowardsCollectionEventLimit(int maxEvents, bool succeeds) {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray(), "INQUIRE PIN\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    var limits = new CollectLimits(16, maxEvents);
    if (succeeds) {
      var result = await client.CollectAsync("ASK", ReadOnlyMemory<byte>.Empty,
        limits, TestContext.Current.CancellationToken);
      Assert.Empty(result.Data);
      Assert.True(client.IsUsable);
    }
    else {
      var error = await Assert.ThrowsAsync<ClientException>(() => client.CollectAsync("ASK",
        ReadOnlyMemory<byte>.Empty, limits, TestContext.Current.CancellationToken));
      Assert.Equal(ClientErrorKind.Limit, error.Kind);
      Assert.False(client.IsUsable);
    }

    Assert.Equal("ASK\nCAN\n"u8.ToArray(), peer.WrittenBytes);
  }

  [Fact]
  public async Task SeparateCollectedCommandsEachCancelTheirOwnInquiry() {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray(),
      "INQUIRE FIRST\nOK\n"u8.ToArray(), "INQUIRE SECOND\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    var result = await client.CollectAsync("ASK", ReadOnlyMemory<byte>.Empty,
      new CollectLimits(32, 3), TestContext.Current.CancellationToken);
    Assert.Empty(result.Data);
    var second = await client.CollectAsync("AGAIN", ReadOnlyMemory<byte>.Empty,
      new CollectLimits(32, 3), TestContext.Current.CancellationToken);
    Assert.Empty(second.Data);
    Assert.Equal("ASK\nCAN\nAGAIN\nCAN\n"u8.ToArray(), peer.WrittenBytes);
  }
}
