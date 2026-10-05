// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Transport;

public sealed class ChannelWriteTests {
  [Fact]
  public async Task WritesExactlyOneValidatedLineAndFlushes() {
    var stream = new ScriptedDuplexStream([]);
    await using var channel = new AssuanChannel(stream);
    var caller = "D a%0Axy\r\n"u8.ToArray();
    await channel.WriteLineAsync(caller, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(caller, stream.Written);
    Assert.Equal(1, stream.WriteCalls);
    Assert.Equal(1, stream.FlushCalls);
    Assert.True(ChannelReadTests.GetBuffer(channel, "_write").AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.True(caller.AsSpan().SequenceEqual("D a%0Axy\r\n"u8));
  }

  [Fact]
  public async Task RejectsMalformedOrOversizedLinesBeforeAnyIo() {
    var token = TestContext.Current.CancellationToken;
    foreach (var bytes in new[] {
               Array.Empty<byte>(), "NOP"u8.ToArray(), "NOP\nBYE\n"u8.ToArray(),
               "D \0\n"u8.ToArray(), "D \rX\n"u8.ToArray(), new byte[1001]
             }) {
      var stream = new ScriptedDuplexStream([]);
      await using var channel = new AssuanChannel(stream);
      var error = await Assert.ThrowsAsync<TransportException>(() => channel.WriteLineAsync(bytes, TimeSpan.FromSeconds(2), token));
      Assert.Equal(TransportErrorKind.Framing, error.Kind);
      Assert.Equal(0, stream.WriteCalls);
      Assert.Empty(stream.Written);
      Assert.True(stream.Disposed);
    }
  }

  [Fact]
  public async Task PartialDeliveryFollowedByIoFailureClosesWithoutRetryingOrLeakingCause() {
    var stream = new ScriptedDuplexStream([]) { FailAfterPrefix = true };
    await using var channel = new AssuanChannel(stream);
    var error = await Assert.ThrowsAsync<TransportException>(() => channel.WriteLineAsync(
      "NOP\n"u8.ToArray(), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
    Assert.Equal(TransportErrorKind.Io, error.Kind);
    Assert.NotNull(error.IoCause);
    Assert.DoesNotContain("SECRET_MARKER", error.ToString());
    Assert.Equal(1, stream.WriteCalls);
    Assert.Single(stream.Written);
    Assert.True(stream.Disposed);
    Assert.True(ChannelReadTests.GetBuffer(channel, "_write").AsSpan().IndexOfAnyExcept((byte)0) < 0);
  }

  [Fact]
  public async Task ZeroTimeoutPreventsEvenImmediatelyReadyIoAndClosesChannel() {
    var stream = new ScriptedDuplexStream("OK\n"u8.ToArray());
    await using var channel = new AssuanChannel(stream);
    var error = await Assert.ThrowsAsync<TransportException>(() => channel.ReadLineAsync(TimeSpan.Zero,
      TestContext.Current.CancellationToken));
    Assert.Equal(TransportErrorKind.Timeout, error.Kind);
    Assert.Equal(0, stream.ReadCalls);
    Assert.True(stream.Disposed);
  }
}
