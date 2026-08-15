// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Tests.Platform.TestHelpers;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Platform.Common.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class TcpClientListenerTests {
  [Fact]
  public void Endpoint_ShouldReturnConfiguredEndpoint() {
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());
    var listener = new TcpClientListener(endpoint, AssuanListenerOptions.Default);

    listener.Endpoint.ShouldBe(endpoint);
  }

  [Fact]
  public async Task Accept_ShouldReturnConnectedConnection_WhenClientConnects() {
    var port = LoopbackHarness.GetFreeTcpPort();
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)port);
    var listener = new TcpClientListener(endpoint, AssuanListenerOptions.Default);

    var acceptTask = Task.Run(() => listener.Accept());

    using var client = new TcpClient();
    await Task.Delay(50);
    await client.ConnectAsync(IPAddress.Loopback, port);

    var connection = await acceptTask;
    await using var _ = connection;

    connection.IsConnected.ShouldBeTrue();

    client.GetStream().Write("hello"u8);
    var result = connection.Read();
    result.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public async Task AcceptAsync_ShouldReturnConnectedConnection_WhenClientConnects() {
    var port = LoopbackHarness.GetFreeTcpPort();
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)port);
    var listener = new TcpClientListener(endpoint, AssuanListenerOptions.Default);

    var acceptTask = listener.AcceptAsync().AsTask();

    using var client = new TcpClient();
    await Task.Delay(50);
    await client.ConnectAsync(IPAddress.Loopback, port);

    var connection = await acceptTask;
    await using var _ = connection;

    connection.IsConnected.ShouldBeTrue();
  }

  [Fact]
  public async Task Accept_ShouldApplyConfiguredStabilizationOptions() {
    var port = LoopbackHarness.GetFreeTcpPort();
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)port);

    var listenerOptions = new AssuanListenerOptions {
      ConfigureStabilization = options => {
        options.Delay = TimeSpan.FromMilliseconds(10);
        options.PollInterval = TimeSpan.FromMilliseconds(5);
      }
    };

    var listener = new TcpClientListener(endpoint, listenerOptions);
    var acceptTask = Task.Run(() => listener.Accept());

    using var client = new TcpClient();
    await Task.Delay(50);
    await client.ConnectAsync(IPAddress.Loopback, port);

    var connection = await acceptTask;
    await using var _ = connection;

    client.GetStream().Write("hi"u8);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var result = connection.Read();
    sw.Stop();

    result.ShouldBe("hi"u8.ToArray());
    sw.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(500));
  }
}
