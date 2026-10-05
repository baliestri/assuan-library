// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class CommandTests {
  [Fact]
  public async Task OneCommandOwnsTheConnectionUntilFinalOrAbandonment() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    var transaction = await client.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    Assert.Equal("NOP\n"u8.ToArray(), stream.WrittenBytes);
    Assert.False(client.IsUsable);
    var error = await Assert.ThrowsAsync<ClientException>(() => client.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.State, error.Kind);
    Assert.Equal("NOP\n"u8.ToArray(), stream.WrittenBytes);
    await transaction.DisposeAsync();
    Assert.True(stream.Closed);
  }

  [Fact]
  public async Task ExtraBufferedGreetingResponsePreventsCommandBeforeAnyWrite() {
    var stream = new ScriptedClientStream("OK\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<ClientException>(() => client.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Protocol, error.Kind);
    Assert.Empty(stream.WrittenBytes);
    Assert.False(client.IsUsable);
  }

  [Fact]
  public async Task InvalidLocalCommandDoesNotWriteOrConsumeReadyClient() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<ClientException>(() => client.CommandAsync("bad name",
      ReadOnlyMemory<byte>.Empty, PayloadSensitivity.Public, TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Protocol, error.Kind);
    Assert.True(client.IsUsable);
    Assert.Empty(stream.WrittenBytes);
    var longName = await Assert.ThrowsAsync<ClientException>(() => client.CommandAsync(new string('X', 1000),
      ReadOnlyMemory<byte>.Empty, PayloadSensitivity.Public, TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Protocol, longName.Kind);
    Assert.True(client.IsUsable);
  }
}
