// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Transport;

public sealed class TcpTests {
  [Fact]
  public async Task LoopbackConnectAndAcceptPreserveBinaryBytesWithoutPeerIdentity() {
    var token = TestContext.Current.CancellationToken;
    var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    Assert.NotEqual(0, listener.Endpoint.Port);
    var accepting = listener.AcceptAsync(token);
    await using var client = await TcpConnector.ConnectAsync(listener.Endpoint, cancellationToken: token);
    var accepted = await accepting;
    await using var peer = accepted.Stream;
    Assert.Null(accepted.PeerIdentity);
    Assert.True(peer is { CanRead: true, CanWrite: true });
    await client.WriteAsync(new byte[] { 0, 0xff, (byte)'\n' }.AsMemory(), token);
    var bytes = new byte[3];
    await peer.ReadExactlyAsync(bytes, token);
    Assert.Equal(new byte[] { 0, 0xff, (byte)'\n' }, bytes);

    await listener.DisposeAsync();
    await peer.WriteAsync(new[] { (byte)'x' }.AsMemory(), token);
    var reply = new byte[1];
    await client.ReadExactlyAsync(reply, token);
    Assert.Equal((byte)'x', reply[0]);
  }

  [Fact]
  public async Task RejectsInvalidTcpDestinationsAndTimeoutsBeforeConnecting() {
    var token = TestContext.Current.CancellationToken;
    foreach (var endpoint in new[] {
               new IPEndPoint(IPAddress.Loopback, 0),
               new IPEndPoint(IPAddress.Any, 12345), new IPEndPoint(IPAddress.IPv6Any, 12345)
             }) {
      var error = await Assert.ThrowsAsync<TransportException>(() => TcpConnector.ConnectAsync(endpoint, cancellationToken: token));
      Assert.Equal(TransportErrorKind.InvalidEndpoint, error.Kind);
    }

    var address = new IPEndPoint(IPAddress.Loopback, 12345);
    Assert.Equal(TransportErrorKind.Timeout,
      (await Assert.ThrowsAsync<TransportException>(() => TcpConnector.ConnectAsync(address, TimeSpan.Zero, token))).Kind);
    foreach (var invalid in new[] { TimeSpan.FromTicks(-1), TimeSpan.MaxValue }) {
      Assert.Equal(TransportErrorKind.InvalidOptions,
        (await Assert.ThrowsAsync<TransportException>(() => TcpConnector.ConnectAsync(address, invalid, token))).Kind);
    }

    using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
    await cancelled.CancelAsync();
    Assert.Equal(TransportErrorKind.Cancelled,
      (await Assert.ThrowsAsync<TransportException>(() => TcpConnector.ConnectAsync(address,
        cancellationToken: cancelled.Token))).Kind);
  }

  [Fact]
  public async Task RetainsAnIoCauseWithoutFormattingItsExternalMessage() {
    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    var error = Assert.Throws<TransportException>(() => TcpAcceptor.Bind(listener.Endpoint));
    Assert.Equal(TransportErrorKind.Io, error.Kind);
    Assert.IsType<SocketException>(error.IoCause);
    Assert.DoesNotContain(error.IoCause.Message, error.ToString());
  }

  [Fact]
  public async Task CancellingAcceptDoesNotCloseTheListener() {
    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    await cancelled.CancelAsync();
    var error = await Assert.ThrowsAsync<TransportException>(async () => await listener.AcceptAsync(cancelled.Token));
    Assert.Equal(TransportErrorKind.Cancelled, error.Kind);
    var accepting = listener.AcceptAsync(TestContext.Current.CancellationToken);
    await using var client = await TcpConnector.ConnectAsync(listener.Endpoint,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var accepted = (await accepting).Stream;
    Assert.True(accepted.CanRead);
  }
}
