// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class InquiryCompatibilityTests {
  [Fact]
  public async Task CommandInquiryWritesDataAndEndBeforeReadingFinalOk() {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray(), "INQUIRE PIN prompt%20\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("ASK", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    var token = TestContext.Current.CancellationToken;
    using (var ev = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.Inquire, ev.Kind);
      Assert.Equal("PIN"u8.ToArray(), ev.Inquiry.KeywordBytes.ToArray());
      Assert.Equal("prompt%20"u8.ToArray(), ev.Inquiry.Arguments.ToArray());
      await Assert.ThrowsAsync<InvalidOperationException>(() => tx.ReadNextAsync(token));
      await ev.Inquiry.SendDataAsync("a\n%"u8.ToArray(), token);
      Assert.Equal("prompt%20"u8.ToArray(), ev.Inquiry.Arguments.ToArray());
      await ev.Inquiry.FinishAsync(token);
      Assert.Equal("ASK\nD a%0A%25\nEND\n"u8.ToArray(), peer.WrittenBytes);
    }

    var final = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(token));
    Assert.Equal(ClientEventKind.Finished, final.Kind);
    final.Dispose();
    tx.Finish();
    Assert.True(client.IsUsable);
  }

  [Fact]
  public async Task CancelInquiryStillRequiresRemoteFinalAndLeavesCompletedErrReusable() {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray(),
      "INQUIRE PIN\nERR 7 cancelled\n"u8.ToArray(), "OK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    await using (var tx = await client.CommandAsync("ASK", ReadOnlyMemory<byte>.Empty,
                   PayloadSensitivity.Public, TestContext.Current.CancellationToken)) {
      using (var ev = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken))) {
        await ev.Inquiry.CancelAsync(TestContext.Current.CancellationToken);
      }

      var final = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
      Assert.Equal((uint)7, final.RemoteCode);
      final.Dispose();
      var error = Assert.Throws<ClientException>(tx.Finish);
      Assert.Equal(ClientErrorKind.Remote, error.Kind);
    }

    Assert.True(client.IsUsable);
    Assert.Equal("ASK\nCAN\n"u8.ToArray(), peer.WrittenBytes);
  }

  [Fact]
  public async Task DroppedUnansweredInquiryInvalidatesTheClient() {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray(), "INQUIRE PIN\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("ASK", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Secret, TestContext.Current.CancellationToken);
    var ev = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Throws<InvalidOperationException>(() => _ = ev.PayloadBytes);
    ev.Dispose();
    Assert.False(client.IsUsable);
    Assert.True(peer.Closed);
    Assert.DoesNotContain("PIN", ev.ToString());
  }

  [Fact]
  public async Task TwoInquiriesInOneCommandCanEachEndBeforeTheFinal() {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray(),
      "INQUIRE FIRST\nINQUIRE SECOND\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("ASK", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    foreach (var keyword in new[] { "FIRST"u8.ToArray(), "SECOND"u8.ToArray() }) {
      using var ev = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
      Assert.Equal(keyword, ev.Inquiry.KeywordBytes.ToArray());
      await ev.Inquiry.FinishAsync(TestContext.Current.CancellationToken);
    }

    var final = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
    final.Dispose();
    tx.Finish();
    Assert.Equal("ASK\nEND\nEND\n"u8.ToArray(), peer.WrittenBytes);
  }

  [Fact]
  public async Task CancelledCanWriteInvalidatesAndDefersItsBorrowedWireBuffer() {
    var peer = new PendingCanStream();
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("ASK", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    using var ev = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
    using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var pending = ev.Inquiry.CancelAsync(cancelled.Token);
    await peer._started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    await cancelled.CancelAsync();
    var error = await Assert.ThrowsAsync<ClientException>(() => pending);
    Assert.Equal(ClientErrorKind.Cancelled, error.Kind);
    Assert.Equal("CAN\n"u8.ToArray(), peer.Pending.ToArray());
    Assert.False(client.IsUsable);
    peer.Finish();
    Assert.True(SpinWait.SpinUntil(() => peer.Pending.Span.IndexOfAnyExcept((byte)0) < 0,
      TimeSpan.FromSeconds(2)));
  }

  private sealed class PendingCanStream : ScriptedClientStream {
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal PendingCanStream() : base("OK\n"u8.ToArray(), "INQUIRE PIN\n"u8.ToArray()) { }
    internal ReadOnlyMemory<byte> Pending { get; private set; }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) {
      if (!buffer.Span.SequenceEqual("CAN\n"u8)) {
        return base.WriteAsync(buffer, token);
      }

      Pending = buffer;
      _started.SetResult();
      return new ValueTask(_release.Task); // Deliberately ignores cancellation.
    }

    internal void Finish()
      => _release.SetResult();
  }
}
