// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Windows;

namespace AssuanLibrary.Tests.Transport;

public sealed class WindowsPipeTests {
  [Fact]
  public async Task OneListenerAcceptsSuccessiveBinaryConnections() {
    if (!OperatingSystem.IsWindows()) return;
    var endpoint = NewEndpoint();
    await using var listener = PipeAcceptor.Bind(endpoint);
    var token = TestContext.Current.CancellationToken;
    foreach (var value in new byte[] { 1, 2 }) {
      var accepting = listener.AcceptAsync(token);
      using var client = await PipeConnector.ConnectAsync(endpoint, cancellationToken: token);
      var admitted = await accepting;
      using var server = admitted.Stream;
      Assert.Null(admitted.PeerIdentity);
      await client.WriteAsync(new byte[] { 0, 0xff, value }.AsMemory(), token);
      byte[] bytes = new byte[3];
      await server.ReadExactlyAsync(bytes, token);
      Assert.Equal(new byte[] { 0, 0xff, value }, bytes);
    }
  }

  [Fact]
  public async Task BusyPipeTimesOutWithoutLeavingALateConnectionWorker() {
    if (!OperatingSystem.IsWindows()) return;
    var endpoint = NewEndpoint();
    await using var listener = PipeAcceptor.Bind(endpoint);
    var token = TestContext.Current.CancellationToken;
    using var first = await PipeConnector.ConnectAsync(endpoint, cancellationToken: token);
    var error = await Assert.ThrowsAsync<TransportException>(() =>
      PipeConnector.ConnectAsync(endpoint, TimeSpan.FromMilliseconds(80), token));
    Assert.Equal(TransportErrorKind.Timeout, error.Kind);
    var accepting = listener.AcceptAsync(token);
    using var acceptedFirst = (await accepting).Stream;
    var acceptingNext = listener.AcceptAsync(token);
    using var second = await PipeConnector.ConnectAsync(endpoint, cancellationToken: token);
    using var acceptedSecond = (await acceptingNext).Stream;
    await second.WriteAsync("second"u8.ToArray().AsMemory(), token);
    byte[] bytes = new byte[6];
    await acceptedSecond.ReadExactlyAsync(bytes, token);
    Assert.True(bytes.AsSpan().SequenceEqual("second"u8));
  }

  [Fact]
  public async Task CancelledAcceptRemainsUsableAndFlushDoesNotWaitForPeerConsumption() {
    if (!OperatingSystem.IsWindows()) return;
    var endpoint = NewEndpoint();
    await using var listener = PipeAcceptor.Bind(endpoint);
    var token = TestContext.Current.CancellationToken;
    using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
    cancelled.Cancel();
    var error = await Assert.ThrowsAsync<TransportException>(async () =>
      await listener.AcceptAsync(cancelled.Token));
    Assert.Equal(TransportErrorKind.Cancelled, error.Kind);
    var accepting = listener.AcceptAsync(token);
    var client = await PipeConnector.ConnectAsync(endpoint, cancellationToken: token);
    using var server = (await accepting).Stream;
    await client.WriteAsync("unread data"u8.ToArray().AsMemory(), token);
    await client.FlushAsync(token).WaitAsync(TimeSpan.FromSeconds(1), token);
    client.Dispose();
    byte[] received = new byte[11];
    await server.ReadExactlyAsync(received, token);
    Assert.True(received.AsSpan().SequenceEqual("unread data"u8));
    try {
      byte[] tail = new byte[1];
      Assert.Equal(0, await server.ReadAsync(tail.AsMemory(), token).AsTask().WaitAsync(TimeSpan.FromSeconds(1), token));
    } catch (IOException) {
      // A closed pipe can report either EOF or broken pipe depending on the OS.
    }
  }

  [Fact]
  public async Task CancellingAnInFlightAcceptDoesNotStealTheNextConnection() {
    if (!OperatingSystem.IsWindows()) return;
    var endpoint = NewEndpoint();
    await using var listener = PipeAcceptor.Bind(endpoint);
    var token = TestContext.Current.CancellationToken;
    using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
    var pending = listener.AcceptAsync(cancelled.Token);
    cancelled.CancelAfter(TimeSpan.FromMilliseconds(30));
    var error = await Assert.ThrowsAsync<TransportException>(async () => await pending);
    Assert.Equal(TransportErrorKind.Cancelled, error.Kind);
    var next = listener.AcceptAsync(token);
    using var client = await PipeConnector.ConnectAsync(endpoint, cancellationToken: token);
    using var peer = (await next).Stream;
    await client.WriteAsync(new byte[] { 42 }.AsMemory(), token);
    var received = new byte[1];
    await peer.ReadExactlyAsync(received, token);
    Assert.Equal((byte)42, received[0]);
  }

  private static LocalEndpoint NewEndpoint() => LocalEndpoint.NamedPipe(
    @"\\.\pipe\assuan-ipc-" + Guid.NewGuid().ToString("N"));
}
