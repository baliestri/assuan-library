// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Transport;

public sealed class ChannelReadTests {
  [Fact]
  public async Task ReadsCoalescedLinesFromReadAheadWithoutAnotherStreamRead() {
    var stream = new ScriptedDuplexStream("D hello%0A\r\nOK\n"u8.ToArray());
    await using var channel = new AssuanChannel(stream);
    var token = TestContext.Current.CancellationToken;
    Assert.True(await channel.ReadLineOrEofAsync(TimeSpan.FromSeconds(2), token));
    Assert.True(channel.Line.SequenceEqual("D hello%0A"u8));
    Assert.True(channel.HasBufferedInput);
    Assert.Equal(1, stream.ReadCalls);
    channel.MutableLine.Fill((byte)'x');
    Assert.True(await channel.ReadLineOrEofAsync(TimeSpan.FromSeconds(2), token));
    Assert.True(channel.Line.SequenceEqual("OK"u8));
    Assert.False(channel.HasBufferedInput);
    Assert.Equal(1, stream.ReadCalls);
    Assert.True(GetBuffer(channel, "_ahead").AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.False(await channel.ReadLineOrEofAsync(TimeSpan.FromSeconds(2), token));
    Assert.True(channel.Line.IsEmpty);
  }

  [Fact]
  public async Task AcceptsACompleteWireLineAtExactlyOneThousandBytes() {
    var wire = new byte[1000];
    Array.Fill(wire, (byte)'x');
    wire[^1] = (byte)'\n';
    await using var channel = new AssuanChannel(new ScriptedDuplexStream(wire));
    await channel.ReadLineAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    Assert.Equal(999, channel.Line.Length);
  }

  [Fact]
  public async Task ReassemblesFragmentedLinesAndDistinguishesEmptyReadyLine() {
    var stream = new ScriptedDuplexStream("D a%0A\n\n"u8.ToArray(), 2);
    await using var channel = new AssuanChannel(stream);
    var token = TestContext.Current.CancellationToken;
    Assert.True(await channel.ReadLineOrEofAsync(TimeSpan.FromSeconds(2), token));
    Assert.True(channel.Line.SequenceEqual("D a%0A"u8));
    Assert.True(await channel.ReadLineOrEofAsync(TimeSpan.FromSeconds(2), token));
    Assert.True(channel.Line.IsEmpty);
    Assert.False(await channel.ReadLineOrEofAsync(TimeSpan.FromSeconds(2), token));
  }

  [Fact]
  public async Task OptionalEofIsCleanOnlyAtBoundaryAndRequiredReadRejectsIt() {
    var token = TestContext.Current.CancellationToken;
    await using var empty = new AssuanChannel(new ScriptedDuplexStream([]));
    Assert.False(await empty.ReadLineOrEofAsync(TimeSpan.FromSeconds(1), token));
    await using var partial = new AssuanChannel(new ScriptedDuplexStream("unfinished"u8.ToArray()));
    var error = await Assert.ThrowsAsync<TransportException>(() => partial.ReadLineOrEofAsync(TimeSpan.FromSeconds(1), token));
    Assert.Equal(TransportErrorKind.Framing, error.Kind);
    Assert.Equal(ProtocolErrorKind.UnexpectedEof, error.ProtocolCause);
    await using var required = new AssuanChannel(new ScriptedDuplexStream([]));
    error = await Assert.ThrowsAsync<TransportException>(() => required.ReadLineAsync(TimeSpan.FromSeconds(1), token));
    Assert.Equal(TransportErrorKind.Framing, error.Kind);
    Assert.Equal(ProtocolErrorKind.UnexpectedEof, error.ProtocolCause);
  }

  [Fact]
  public async Task FramingOverflowClosesChannelAndWipesBuffersRegardlessOfClassification() {
    var bytes = new byte[1001];
    Array.Fill(bytes, (byte)'x');
    bytes[^1] = (byte)'\n';
    var stream = new ScriptedDuplexStream(bytes);
    await using var channel = new AssuanChannel(stream);
    var error = await Assert.ThrowsAsync<TransportException>(()
      => channel.ReadLineAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
    Assert.Equal(TransportErrorKind.Framing, error.Kind);
    Assert.Equal(ProtocolErrorKind.LineTooLong, error.ProtocolCause);
    Assert.True(stream.Disposed);
    Assert.True(GetBuffer(channel, "_ahead").AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.Throws<TransportException>(() => _ = channel.Line);
  }

  internal static byte[] GetBuffer(AssuanChannel channel, string field) =>
    Assert.IsType<byte[]>(typeof(AssuanChannel).GetField(field,
      BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(channel));
}
