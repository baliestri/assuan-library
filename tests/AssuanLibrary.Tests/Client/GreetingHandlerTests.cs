// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Client;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Tests.Client;

public sealed class GreetingHandlerTests {
  [Fact]
  public async Task DefaultGreetingCancelsInquiryAndStillWaitsForFinalOk() {
    var peer = new ScriptedClientStream("INQUIRE TOKEN\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("CAN\n"u8.ToArray(), peer.WrittenBytes);
    Assert.True(client.IsUsable);
  }

  [Fact]
  public async Task DefaultCanStillRejectGreetingWithFullRemoteCode() {
    var peer = new ScriptedClientStream("INQUIRE TOKEN\nERR 4294967295 denied\n"u8.ToArray());
    var error = await Assert.ThrowsAsync<ClientException>(() => AssuanClient.FromStreamAsync(peer,
      cancellationToken: TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.GreetingRejected, error.Kind);
    Assert.Equal(uint.MaxValue, error.RemoteCode);
    Assert.Equal("CAN\n"u8.ToArray(), peer.WrittenBytes);
    Assert.True(peer.Closed);
  }

  [Fact]
  public async Task ResolvedNoncePrecedesTheDefaultGreetingInquiryCancel() {
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    using var nonce = new SecretBuffer(16);
    nonce.Append(Enumerable.Range(0, 16).Select(value => (byte)value).ToArray());
    using var resolved = ResolvedAgentEndpoint.ForWindows((ushort)((IPEndPoint)listener.LocalEndpoint).Port, nonce);
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(2));
    var accepting = listener.AcceptTcpClientAsync(timeout.Token);
    var connecting = AssuanClient.ConnectAsync(resolved, cancellationToken: timeout.Token);
    using var peer = await accepting;
    var bytes = new byte[16];
    await peer.GetStream().ReadExactlyAsync(bytes, timeout.Token);
    Assert.Equal(nonce.Expose().ToArray(), bytes);
    await peer.GetStream().WriteAsync("INQUIRE TOKEN\nOK\n"u8.ToArray(), timeout.Token);
    var cancel = new byte[4];
    await peer.GetStream().ReadExactlyAsync(cancel, timeout.Token);
    Assert.Equal("CAN\n"u8.ToArray(), cancel);
    await using var client = await connecting;
    Assert.True(client.IsUsable);
  }

  [Fact]
  public async Task CallbackCanSendDataAndFinishBeforeGreetingOk() {
    var peer = new ScriptedClientStream("INQUIRE TOKEN\nOK\n"u8.ToArray());
    var callback = new Callback(async (inquiry, token) => {
      await inquiry.SendDataAsync("a\n%"u8.ToArray(), token);
      await inquiry.FinishAsync(token);
    });
    await using var client = await AssuanClient.FromStreamAsync(peer, callback,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("D a%0A%25\nEND\n"u8.ToArray(), peer.WrittenBytes);
    Assert.True(client.IsUsable);
  }

  [Fact]
  public async Task AbandonedOrThrowingCallbackClosesWithoutExposingItsText() {
    var peer = new ScriptedClientStream("INQUIRE TOKEN\nOK\n"u8.ToArray());
    var error = await Assert.ThrowsAsync<ClientException>(() => AssuanClient.FromStreamAsync(peer,
      new Callback((_, _) => ValueTask.CompletedTask), cancellationToken: TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Incomplete, error.Kind);
    Assert.True(peer.Closed);
    Assert.Empty(peer.WrittenBytes);

    var throwing = new ScriptedClientStream("INQUIRE TOKEN\nOK\n"u8.ToArray());
    var failure = await Assert.ThrowsAsync<ClientException>(() => AssuanClient.FromStreamAsync(throwing,
      new Callback((_, _) => ValueTask.FromException(new IOException("SECRET_MARKER"))),
      cancellationToken: TestContext.Current.CancellationToken));
    Assert.DoesNotContain("SECRET_MARKER", failure.ToString());
    Assert.True(throwing.Closed);
  }

  [Fact]
  public async Task TimedOutNoncooperativeCallbackKeepsMetadataUntilItsTaskFinishes() {
    var peer = new ScriptedClientStream("INQUIRE TOKEN prompt\nOK\n"u8.ToArray());
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    ClientInquiry? borrowed = null;
    var callback = new Callback(async (inquiry, _) => {
      borrowed = inquiry;
      started.SetResult();
      await release.Task; // Ignores cancellation deliberately.
    });
    var opening = AssuanClient.FromStreamAsync(peer, callback,
      new ClientOptions(greetingTimeout: TimeSpan.FromMilliseconds(100)), TestContext.Current.CancellationToken);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<ClientException>(() => opening);
    Assert.Equal(ClientErrorKind.Timeout, error.Kind);
    Assert.True(peer.Closed);
    Assert.Equal("prompt"u8.ToArray(), borrowed!.Arguments.ToArray());
    release.SetResult();
    Assert.True(SpinWait.SpinUntil(() => {
      try {
        _ = borrowed.Arguments.Length;
        return false;
      }
      catch (ObjectDisposedException) { return true; }
    }, TimeSpan.FromSeconds(2)));
  }

  private sealed class Callback(Func<ClientInquiry, CancellationToken, ValueTask> respond)
    : IGreetingInquiryHandler {
    public ValueTask RespondAsync(ClientInquiry inquiry, CancellationToken token) => respond(inquiry, token);
  }
}
