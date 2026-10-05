// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Transport;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Unix;

namespace AssuanLibrary.Tests.Server;

public sealed class ServerInteropTests {
  [Fact]
  public async Task TwoLiteralLoopbackPeersCanFinishIndependently() {
    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    var host = new AssuanServer<object>(() => new object(), new ServerOptions(maxSessions: 2));
    using var stop = new CancellationTokenSource();
    var serving = host.ServeAsync(listener, stop.Token);
    using var first = new TcpClient(AddressFamily.InterNetwork);
    using var second = new TcpClient(AddressFamily.InterNetwork);
    var token = TestContext.Current.CancellationToken;
    await first.ConnectAsync(listener.Endpoint.Address, listener.Endpoint.Port, token);
    await second.ConnectAsync(listener.Endpoint.Address, listener.Endpoint.Port, token);
    var greeting = new byte[3];
    await first.GetStream().ReadExactlyAsync(greeting, token);
    Assert.Equal("OK\n"u8.ToArray(), greeting);
    await second.GetStream().ReadExactlyAsync(greeting, token);
    Assert.Equal("OK\n"u8.ToArray(), greeting);
    await second.GetStream().WriteAsync("NOP\nBYE\n"u8.ToArray(), token);
    var secondResults = new byte[6];
    await second.GetStream().ReadExactlyAsync(secondResults, token);
    Assert.Equal("OK\nOK\n"u8.ToArray(), secondResults);
    await first.GetStream().WriteAsync("BYE\n"u8.ToArray(), token);
    await first.GetStream().ReadExactlyAsync(greeting, token);
    Assert.Equal("OK\n"u8.ToArray(), greeting);
    await host.WhenDrainedAsync(token).WaitAsync(TimeSpan.FromSeconds(2), token);
    await stop.CancelAsync();
    await serving;
    Assert.Equal(0, host.PendingSessionCount);
  }

  [Fact]
  public async Task UnixSocketPathStillBelongsToListenerAfterHostShutdown() {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new UnixFixture();
    var path = fixture.Socket();
    var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(path));
    var host = new AssuanServer<object>(() => new object(), ServerOptions.Default);
    using var stop = new CancellationTokenSource();
    var serving = host.ServeAsync(listener, stop.Token);
    await stop.CancelAsync();
    await serving;
    Assert.True(UnixNative.TryInspectNoFollow(path, out var _));
    listener.Cleanup();
    Assert.False(UnixNative.TryInspectNoFollow(path, out var _));
  }
}
