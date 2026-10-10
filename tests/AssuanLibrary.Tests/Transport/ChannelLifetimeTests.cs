// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Transport;

public sealed class ChannelLifetimeTests {
  [Fact]
  public async Task ClosingTwiceWipesBuffersAndRejectsLaterOperations() {
    var stream = new ScriptedDuplexStream("OK\n"u8.ToArray());
    await using var channel = new AssuanChannel(stream);
    await channel.ReadLineAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
    channel.Close();
    channel.Close();
    Assert.True(stream.Disposed);
    Assert.True(ChannelReadTests.GetBuffer(channel, "_ahead").AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.True(ChannelReadTests.GetBuffer(channel, "_write").AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.Equal(TransportErrorKind.Closed, (await Assert.ThrowsAsync<TransportException>(() => channel.WriteLineAsync("NOP\n"u8.ToArray(),
      TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken))).Kind);
  }

  [Fact]
  public async Task TimedOutNonCooperativeReadKeepsItsBufferUntilTheUnderlyingIoFinishes() {
    var stream = new NonCooperativeStream();
    await using var channel = new AssuanChannel(stream);
    var token = TestContext.Current.CancellationToken;
    var reading = channel.ReadLineOrEofAsync(TimeSpan.FromMilliseconds(150), token);
    await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
    var error = await Assert.ThrowsAsync<TransportException>(() => reading);
    Assert.Equal(TransportErrorKind.Timeout, error.Kind);
    Assert.True(stream.Disposed);
    var ahead = ChannelReadTests.GetBuffer(channel, "_ahead");
    Assert.Equal((byte)'s', ahead[0]); // ReadAsync still owns this memory.
    Assert.Equal(TransportErrorKind.Closed,
      (await Assert.ThrowsAsync<TransportException>(() => channel.ReadLineOrEofAsync(TimeSpan.FromSeconds(1), token))).Kind);
    stream.ReleaseRead("OK\n"u8);
    Assert.True(await Eventually.UntilAsync(() => ahead.AsSpan().IndexOfAnyExcept((byte)0) < 0,
      TimeSpan.FromSeconds(5), token));
  }

  [Fact]
  public async Task TimedOutNonCooperativeWriteKeepsOutgoingBytesUntilCompletion() {
    var stream = new NonCooperativeStream();
    await using var channel = new AssuanChannel(stream);
    var token = TestContext.Current.CancellationToken;
    var writing = channel.WriteLineAsync("NOP\n"u8.ToArray(), TimeSpan.FromMilliseconds(150), token);
    await stream.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
    var error = await Assert.ThrowsAsync<TransportException>(() => writing);
    Assert.Equal(TransportErrorKind.Timeout, error.Kind);
    Assert.True(stream.PendingWrite.Span.SequenceEqual("NOP\n"u8));
    Assert.True(stream.Disposed);
    stream.ReleaseWrite();
    var outgoing = ChannelReadTests.GetBuffer(channel, "_write");
    Assert.True(await Eventually.UntilAsync(() => outgoing.AsSpan().IndexOfAnyExcept((byte)0) < 0,
      TimeSpan.FromSeconds(5), token));
  }
}
