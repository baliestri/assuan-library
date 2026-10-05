// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class HandshakeTests {
  [Fact]
  public async Task ManualHandshakeDoesNoIoAtCreationAndTransfersAfterFinalOk() {
    var peer = new ScriptedClientStream("S READY\nINQUIRE PIN\nOK\n"u8.ToArray(), "OK\n"u8.ToArray());
    await using var handshake = AssuanClient.BeginHandshake(peer);
    Assert.Equal(0, peer.ReadCalls);
    Assert.Empty(peer.WrittenBytes);
    var token = TestContext.Current.CancellationToken;
    using (var status = Assert.IsType<ClientEventLease>(await handshake.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.Status, status.Kind);
      Assert.Equal("READY"u8.ToArray(), status.KeywordBytes.ToArray());
    }

    using (var inquiry = Assert.IsType<ClientEventLease>(await handshake.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.Inquire, inquiry.Kind);
      await inquiry.Inquiry.FinishAsync(token);
    }

    using (var final = Assert.IsType<ClientEventLease>(await handshake.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.Finished, final.Kind);
      Assert.Null(final.RemoteCode);
    }

    Assert.Null(await handshake.ReadNextAsync(token));
    await using var client = handshake.Finish();
    Assert.True(client.IsUsable);
    await using var transaction = await client.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, token);
    var next = Assert.IsType<ClientEventLease>(await transaction.ReadNextAsync(token));
    next.Dispose();
    transaction.Finish();
    Assert.Equal("END\nNOP\n"u8.ToArray(), peer.WrittenBytes);
  }

  [Fact]
  public async Task GreetingErrAfterCanIsFatalWithFullUnsignedCode() {
    var peer = new ScriptedClientStream("INQUIRE PIN\nERR 4294967295 denied\n"u8.ToArray());
    await using var handshake = AssuanClient.BeginHandshake(peer);
    using (var inquiry = Assert.IsType<ClientEventLease>(await handshake.ReadNextAsync(TestContext.Current.CancellationToken))) {
      await inquiry.Inquiry.CancelAsync(TestContext.Current.CancellationToken);
    }

    using (var final = Assert.IsType<ClientEventLease>(await handshake.ReadNextAsync(TestContext.Current.CancellationToken))) {
      Assert.Equal(uint.MaxValue, final.RemoteCode);
    }

    var error = Assert.Throws<ClientException>(handshake.Finish);
    Assert.Equal(ClientErrorKind.GreetingRejected, error.Kind);
    Assert.Equal(uint.MaxValue, error.RemoteCode);
    Assert.True(peer.Closed);
    Assert.Equal("CAN\n"u8.ToArray(), peer.WrittenBytes);
  }

  [Fact]
  public async Task PrematureFinishAndAbandonmentCloseOwnedStream() {
    var peer = new ScriptedClientStream("OK\n"u8.ToArray());
    await using (var handshake = AssuanClient.BeginHandshake(peer)) {
      Assert.Equal(ClientErrorKind.Incomplete, Assert.Throws<ClientException>(handshake.Finish).Kind);
    }

    Assert.True(peer.Closed);
    var another = new ScriptedClientStream("OK\n"u8.ToArray());
    await using (var _ = AssuanClient.BeginHandshake(another)) { }

    Assert.True(another.Closed);
  }
}
