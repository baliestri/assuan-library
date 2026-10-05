// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;

namespace AssuanLibrary.Tests.Client;

public sealed class CollectionTests {
  [Fact]
  public async Task OptInCollectionCountsDecodedBytesMetadataAndEveryEvent() {
    var reply = "D one\nD two\nS X y\n# z\nEND\nOK\n"u8.ToArray();
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(), reply);
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    var result = await client.CollectAsync("GET", ReadOnlyMemory<byte>.Empty,
      new CollectLimits(11, 6), TestContext.Current.CancellationToken);
    Assert.Equal("onetwo"u8.ToArray(), result.Data);
    var status = Assert.Single(result.Statuses);
    Assert.Equal("X"u8.ToArray(), status.KeywordBytes.ToArray());
    Assert.Equal("y"u8.ToArray(), status.Arguments.ToArray());
    Assert.Equal(" z"u8.ToArray(), Assert.Single(result.Comments));
    Assert.True(client.IsUsable);
  }

  [Theory]
  [InlineData(3, 1)]
  [InlineData(2, 2)]
  public async Task ACollectionLimitClosesTheConnection(int bytes, int events) {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(), "D abc\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<ClientException>(() => client.CollectAsync("GET",
      ReadOnlyMemory<byte>.Empty, new CollectLimits(bytes, events), TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Limit, error.Kind);
    Assert.False(client.IsUsable);
  }

  [Fact]
  public async Task SecretCollectionOwnsFixedProtectedDataButMetadataRemainOrdinary() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(),
      "D SECRET_MARKER\nS LABEL public\n# metadata\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    var result = await client.CollectSecretAsync("GET", ReadOnlyMemory<byte>.Empty,
      new CollectLimits(64, 4), TestContext.Current.CancellationToken);
    Assert.Equal("SECRET_MARKER"u8.ToArray(), result.ExposeData().ToArray());
    Assert.DoesNotContain("SECRET_MARKER", result.ToString());
    Assert.Equal("public"u8.ToArray(), Assert.Single(result.Statuses).Arguments.ToArray());
    result.Dispose();
    Assert.Throws<ObjectDisposedException>(() => _ = result.ExposeData().Length);
    Assert.Equal(" metadata"u8.ToArray(), Assert.Single(result.Comments));
  }

  [Fact]
  public async Task RemoteErrAfterDataDoesNotDestroyAReadyClient() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(),
      "D x\nERR 7 denied\n"u8.ToArray(), "OK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<ClientException>(() => client.CollectAsync("FIRST",
      ReadOnlyMemory<byte>.Empty, cancellationToken: TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Remote, error.Kind);
    Assert.Equal((uint)7, error.RemoteCode);
    Assert.True(client.IsUsable);
    var result = await client.CollectAsync("SECOND", ReadOnlyMemory<byte>.Empty,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.Empty(result.Data);
  }
}
