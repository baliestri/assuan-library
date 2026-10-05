// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Tests.Transport;

public sealed class AgentConnectionTests {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task PendingNonceWriteRetainsSecretUntilCompletionAfterTimeoutOrCancellation(bool cancel) {
    var secret = new SecretBuffer(16);
    var nonce = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
    secret.Append(nonce);
    var resolved = ResolvedAgentEndpoint.ForWindows(12345, secret);
    await using var stream = new DelayedNonceStream();
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var connecting = resolved.ConnectWithAsync((_, _, _) => Task.FromResult<Stream>(stream),
      cancel ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(100), cancellation.Token);
    await stream._started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    if (cancel) {
      await cancellation.CancelAsync();
    }

    var error = await Assert.ThrowsAsync<TransportException>(() => connecting);
    Assert.Equal(cancel ? TransportErrorKind.Cancelled : TransportErrorKind.Timeout, error.Kind);
    Assert.True(stream._closed);
    resolved.Dispose();
    Assert.Equal(nonce, secret.Expose().ToArray()); // Still borrowed by the pending write.
    Assert.Equal(nonce, stream.PendingNonce.ToArray());
    stream.Finish();
    Assert.True(SpinWait.SpinUntil(() => {
      try {
        _ = secret.Length;
        return false;
      }
      catch (ObjectDisposedException) { return true; }
    }, TimeSpan.FromSeconds(2)), "Nonce storage was not cleared after its pending write finished.");
  }

  [Fact]
  public async Task NativeWindowsNonceIsSentBeforeReturningStreamWithoutReadingGreeting() {
    if (!OperatingSystem.IsWindows()) {
      return;
    }

    using var fixture = new AgentFixture();
    var nonce = new byte[] { 0, 10, 13, 255, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    File.WriteAllBytes(fixture.SocketFile,
      [.. Encoding.ASCII.GetBytes(port + "\n"), .. nonce]);
    var token = TestContext.Current.CancellationToken;
    var resolved = await new AgentLocator(fixture.Executable, fixture.Home).ResolveAsync(token);
    Assert.Equal(AgentEndpointKind.NativeWindowsAgent, resolved.Kind);
    var accepting = listener.AcceptTcpClientAsync(token);
    var connecting = resolved.ConnectAsync(TimeSpan.FromSeconds(2), token);
    using var peer = await accepting;
    await using var stream = await connecting;
    var received = new byte[16];
    await peer.GetStream().ReadExactlyAsync(received, token);
    Assert.Equal(nonce, received);
    await peer.GetStream().WriteAsync("OK\n"u8.ToArray().AsMemory(), token);
    var greeting = new byte[3];
    await stream.ReadExactlyAsync(greeting, token);
    Assert.Equal("OK\n"u8.ToArray(), greeting);
    Assert.DoesNotContain("255", resolved.ToString());
    Assert.Equal(TransportErrorKind.Timeout,
      (await Assert.ThrowsAsync<TransportException>(() => resolved.ConnectAsync(TimeSpan.Zero, token))).Kind);
    resolved.Dispose();
    await Assert.ThrowsAsync<ObjectDisposedException>(() => resolved.ConnectAsync(cancellationToken: token));
  }

  [Fact]
  public async Task OversizedNativeFileIsRejectedByLocatorWithoutNonceDisclosure() {
    if (!OperatingSystem.IsWindows()) {
      return;
    }

    using var fixture = new AgentFixture();
    File.WriteAllBytes(fixture.SocketFile, new byte[1024]);
    var error = await Assert.ThrowsAsync<DiscoveryException>(() =>
      new AgentLocator(fixture.Executable, fixture.Home).ResolveAsync(TestContext.Current.CancellationToken));
    Assert.Equal(DiscoveryErrorKind.InvalidOutput, error.Kind);
    Assert.DoesNotContain(fixture.SocketFile, error.ToString());
  }

  private sealed class DelayedNonceStream : MemoryStream {
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool _closed;
    internal ReadOnlyMemory<byte> PendingNonce { get; private set; }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
      PendingNonce = buffer;
      _started.SetResult();
      return new ValueTask(_release.Task);
    }

    internal void Finish() => _release.SetResult();

    protected override void Dispose(bool disposing) {
      _closed = true;
      base.Dispose(disposing);
    }
  }
}
