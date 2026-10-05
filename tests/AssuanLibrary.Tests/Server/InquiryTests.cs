// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class InquiryTests {
  private static (CommandContext<object>, AssuanChannel, ServerMachine) Create(
  SessionDuplexStream stream, int limit = 1024 * 1024) {
    var channel = new AssuanChannel(stream);
    var machine = new ServerMachine();
    machine.SendResponse(ProtocolResponseKind.Ok);
    machine.ReceiveRequest(RequestKind.Command);
    var context = new CommandContext<object>(new object(), ReadOnlyMemory<byte>.Empty, null, true,
      channel, machine, new TransportDeadline(TimeSpan.FromSeconds(5)), limit);
    return (context, channel, machine);
  }

  [Fact]
  public async Task SecretChunksKeepTheirLeaseUntilReleasedAndEndOnlyOnFinish() {
    await using var stream = new SessionDuplexStream("# ignored\nD %00%FF\nD \nEND\nNOP\n"u8.ToArray());
    var (context, channel, machine) = Create(stream);
    await using var owned = channel;
    await using var inquiry = await context.BeginInquiryAsync("PIN", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Secret, TestContext.Current.CancellationToken);
    Assert.Equal("INQUIRE PIN\n"u8.ToArray(), stream.WrittenBytes);
    var first = Assert.IsType<InquiryChunk>(await inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Throws<InvalidOperationException>(() => _ = first.Data);
    Assert.Equal(new byte[] { 0, 255 }, first.Expose().ToArray());
    await Assert.ThrowsAsync<InvalidOperationException>(() => inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Throws<InvalidOperationException>(() => inquiry.Finish());
    var borrowed = first.Expose();
    first.Dispose();
    Assert.Equal(new byte[] { 0, 0 }, borrowed.ToArray());
    var empty = Assert.IsType<InquiryChunk>(await inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Empty(empty.Expose().ToArray());
    empty.Dispose();
    Assert.Null(await inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Null(await inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Equal(ServerState.Inquiry, machine.State);
    Assert.Equal(InquiryOutcome.Completed, inquiry.Finish());
    Assert.Equal(ServerState.Handler, machine.State);
    await channel.ReadLineAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    Assert.Equal("NOP"u8.ToArray(), channel.Line.ToArray());
  }

  [Fact]
  public async Task CanIsDifferentFromEndAndAbandonmentClosesTheChannel() {
    await using var stream = new SessionDuplexStream("CAN\n"u8.ToArray());
    var (context, channel, machine) = Create(stream);
    await using var owned = channel;
    await using (var inquiry = await context.BeginInquiryAsync("PIN", ReadOnlyMemory<byte>.Empty,
                   PayloadSensitivity.Public, TestContext.Current.CancellationToken)) {
      Assert.Null(await inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
      Assert.Equal(InquiryOutcome.Cancelled, inquiry.Finish());
    }

    Assert.Equal(ServerState.AwaitFinal, machine.State);

    await using var anotherStream = new SessionDuplexStream(ReadOnlyMemory<byte>.Empty);
    var (anotherContext, anotherChannel, anotherMachine) = Create(anotherStream);
    await using var anotherOwned = anotherChannel;
    await using (var abandoned = await anotherContext.BeginInquiryAsync("PIN", ReadOnlyMemory<byte>.Empty,
                   PayloadSensitivity.Public, TestContext.Current.CancellationToken)) { }

    Assert.Equal(ServerState.Invalid, anotherMachine.State);
    Assert.True(anotherContext.IsClosed);
  }

  [Fact]
  public async Task DecodedBytesCountTowardsTheTotalLimit() {
    await using var stream = new SessionDuplexStream("D %00%FF\nEND\n"u8.ToArray());
    var (context, channel, machine) = Create(stream, 1);
    await using var owned = channel;
    await using var inquiry = await context.BeginInquiryAsync("PIN", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<ServerException>(() => inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Equal(ServerErrorKind.Limit, error.Kind);
    Assert.Equal(ServerState.Invalid, machine.State);
  }

  [Fact]
  public async Task ClosingChannelWhileAChunkIsBorrowedDefersItsWipeUntilRelease() {
    await using var stream = new SessionDuplexStream("D secret\n"u8.ToArray());
    var (context, channel, _) = Create(stream);
    await using var owned = channel;
    await using var inquiry = await context.BeginInquiryAsync("PIN", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Secret, TestContext.Current.CancellationToken);
    var chunk = Assert.IsType<InquiryChunk>(await inquiry.ReadNextAsync(TestContext.Current.CancellationToken));
    var borrowed = chunk.Expose();
    channel.Close();
    Assert.Equal("secret"u8.ToArray(), borrowed.ToArray());
    chunk.Dispose();
    Assert.Equal(new byte[6], borrowed.ToArray());
  }

  [Fact]
  public async Task InvalidSensitivityIsRejectedBeforeSendingAnInquiry() {
    await using var stream = new SessionDuplexStream(ReadOnlyMemory<byte>.Empty);
    var (context, channel, machine) = Create(stream);
    await using var owned = channel;
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.BeginInquiryAsync("PIN",
      ReadOnlyMemory<byte>.Empty, (PayloadSensitivity)42, TestContext.Current.CancellationToken));
    Assert.Empty(stream.WrittenBytes);
    Assert.Equal(ServerState.Handler, machine.State);
  }
}
