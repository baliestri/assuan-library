// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class ContextTests {
  private static (CommandContext<object> Context, AssuanChannel Channel, ServerMachine Machine)
    CreateContext(Stream stream) {
    var channel = new AssuanChannel(stream);
    var machine = new ServerMachine();
    machine.SendResponse(ProtocolResponseKind.Ok);
    machine.ReceiveRequest(RequestKind.Command);
    var context = new CommandContext<object>(new object(), "raw%00"u8.ToArray(), null, true,
      channel, machine, new TransportDeadline(TimeSpan.FromSeconds(5)));
    return (context, channel, machine);
  }

  [Fact]
  public async Task StreamsEscapedDataEmptyDataAndStatusWithoutAFinal() {
    await using var stream = new SessionDuplexStream(ReadOnlyMemory<byte>.Empty);
    var (context, channel, _) = CreateContext(stream);
    await using var owned = channel;
    Assert.Equal("raw%00"u8.ToArray(), context.Arguments.ToArray());
    Assert.True(context.IsAuthenticated);
    Assert.Null(context.PeerIdentity);
    await context.SendDataAsync("first\nsecond"u8.ToArray(), TestContext.Current.CancellationToken);
    await context.SendDataAsync(ReadOnlyMemory<byte>.Empty, TestContext.Current.CancellationToken);
    await context.SendStatusAsync("PROGRESS", "1"u8.ToArray(), TestContext.Current.CancellationToken);
    Assert.Equal("D first%0Asecond\nD \nS PROGRESS 1\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task LargeBinaryPayloadIsSplitWithoutExceedingWireLimit() {
    await using var stream = new SessionDuplexStream(ReadOnlyMemory<byte>.Empty);
    var (context, channel, _) = CreateContext(stream);
    await using var owned = channel;
    await context.SendDataAsync(Enumerable.Repeat((byte)'%', 1000).ToArray(),
      TestContext.Current.CancellationToken);
    var lines = Encoding.ASCII.GetString(stream.WrittenBytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.True(lines.Length > 1);
    Assert.All(lines, line => Assert.True((line.Length + 1) <= 1000));
    Assert.Equal(1000, lines.Sum(line => (line.Length - 2) / 3));
  }

  [Fact]
  public async Task FailedWriteInvalidatesTheContextEvenWhenCallerCatchesIt() {
    await using var stream = new FailingStream();
    var (context, channel, machine) = CreateContext(stream);
    await using var owned = channel;
    await Assert.ThrowsAsync<TransportException>(async () =>
      await context.SendDataAsync("private"u8.ToArray(), TestContext.Current.CancellationToken));
    Assert.Equal(ServerState.Invalid, machine.State);
    await Assert.ThrowsAsync<ServerException>(async () =>
      await context.SendDataAsync("next"u8.ToArray(), TestContext.Current.CancellationToken));
  }

  private sealed class FailingStream : SessionDuplexStream {
    internal FailingStream() : base(ReadOnlyMemory<byte>.Empty) { }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
      => ValueTask.FromException(new IOException("private write failure"));
  }
}
