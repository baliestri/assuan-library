// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Client;

public sealed class InquiryTests {
  private static (ClientInquiry Inquiry, ScriptedClientStream Peer) Create(int limit = 1024 * 1024,
  PayloadSensitivity sensitivity = PayloadSensitivity.Public) {
    var peer = new ScriptedClientStream(ReadOnlyMemory<byte>.Empty);
    var core = new ClientCore(peer);
    core.Machine.Receive(ProtocolResponseKind.Inquire);
    var inquiry = new ClientInquiry(core, new TransportDeadline(TimeSpan.FromSeconds(5)),
      new ClientOptions(maxInquiryBytes: limit), sensitivity, "PIN"u8, "prompt%20"u8);
    return (inquiry, peer);
  }

  [Fact]
  public async Task MetadataSurvivesWritesAndPublicDataIsEscapedWithoutSplitting() {
    var (inquiry, peer) = Create();
    await using (inquiry) {
      Assert.Equal("PIN"u8.ToArray(), inquiry.KeywordBytes.ToArray());
      Assert.Equal("prompt%20"u8.ToArray(), inquiry.Arguments.ToArray());
      await inquiry.SendDataAsync("a\n%"u8.ToArray(), TestContext.Current.CancellationToken);
      await inquiry.SendDataAsync(ReadOnlyMemory<byte>.Empty, TestContext.Current.CancellationToken);
      Assert.Equal("D a%0A%25\nD \n"u8.ToArray(), peer.WrittenBytes);
      Assert.Equal("prompt%20"u8.ToArray(), inquiry.Arguments.ToArray());
      await inquiry.FinishAsync(TestContext.Current.CancellationToken);
      Assert.Equal("D a%0A%25\nD \nEND\n"u8.ToArray(), peer.WrittenBytes);
    }
  }

  [Fact]
  public async Task SecretSendBorrowsCallerBufferAndPreservesBinaryBytes() {
    var (inquiry, peer) = Create(sensitivity: PayloadSensitivity.Secret);
    await using (inquiry) {
      Assert.Throws<InvalidOperationException>(() => _ = inquiry.Arguments);
      Assert.Equal("prompt%20"u8.ToArray(), inquiry.ExposeSecretArguments().ToArray());
      using var secret = new SecretBuffer(4);
      secret.Append([0, 10, 37, 255]);
      await inquiry.SendSecretAsync(secret, TestContext.Current.CancellationToken);
      Assert.Equal(4, secret.Length);
      Assert.Equal([.. "D %00%0A%25"u8.ToArray(), (byte)255, (byte)'\n'], peer.WrittenBytes);
      Assert.DoesNotContain("prompt", inquiry.ToString());
      await inquiry.CancelAsync(TestContext.Current.CancellationToken);
    }
  }

  [Fact]
  public async Task LimitRejectsWholeCallBeforeAnyOfItsBytesAreWritten() {
    var (inquiry, peer) = Create(3);
    await using (inquiry) {
      await inquiry.SendDataAsync("abc"u8.ToArray(), TestContext.Current.CancellationToken);
      var error = await Assert.ThrowsAsync<ClientException>(() => inquiry.SendDataAsync("z"u8.ToArray(),
        TestContext.Current.CancellationToken).AsTask());
      Assert.Equal(ClientErrorKind.Limit, error.Kind);
      Assert.Equal("D abc\n"u8.ToArray(), peer.WrittenBytes);
      Assert.True(peer.Closed);
    }
  }

  [Theory]
  [InlineData(2, 20)]
  [InlineData(20, 2)]
  public async Task InquiryUsesTheSmallerOfParentAndLocalDeadline(int parentSeconds, int localSeconds) {
    var clock = new ManualClock();
    var peer = new ScriptedClientStream(ReadOnlyMemory<byte>.Empty);
    var core = new ClientCore(peer, clock);
    core.Machine.Receive(ProtocolResponseKind.Inquire);
    await using var inquiry = new ClientInquiry(core,
      new TransportDeadline(TimeSpan.FromSeconds(parentSeconds), clock),
      new ClientOptions(inquiryTimeout: TimeSpan.FromSeconds(localSeconds)),
      PayloadSensitivity.Public, "PIN"u8, ReadOnlySpan<byte>.Empty);
    clock.Advance(TimeSpan.FromSeconds(2));
    var error = await Assert.ThrowsAsync<ClientException>(() => inquiry.SendDataAsync("x"u8.ToArray(),
      TestContext.Current.CancellationToken).AsTask());
    Assert.Equal(ClientErrorKind.Timeout, error.Kind);
    Assert.Empty(peer.WrittenBytes);
    Assert.True(peer.Closed);
  }

  private sealed class ManualClock : TimeProvider {
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Volatile.Read(ref _ticks);
    internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
  }
}
