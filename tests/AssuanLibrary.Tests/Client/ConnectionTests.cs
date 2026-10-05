// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Client;
using AssuanLibrary.Protocol;
using AssuanLibrary.Tests.Transport;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;
using AssuanLibrary.Transport.Unix;
using AssuanLibrary.Transport.Windows;

namespace AssuanLibrary.Tests.Client;

public sealed class ConnectionTests {
  [Fact]
  public async Task ExplicitTcpEndpointConsumesGreetingAndSendsNoCommand() {
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var endpoint = (IPEndPoint)listener.LocalEndpoint;
    var accepting = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
    var connecting = AssuanClient.ConnectAsync(endpoint, cancellationToken: TestContext.Current.CancellationToken);
    using var peer = await accepting;
    await peer.GetStream().WriteAsync("OK\n"u8.ToArray(), TestContext.Current.CancellationToken);
    await using var client = await connecting;
    Assert.True(client.IsUsable);
  }

  [Fact]
  public async Task ResolvedNativeSocketConnectSendsNonceBeforeGreeting() {
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
    var received = new byte[16];
    await peer.GetStream().ReadExactlyAsync(received, timeout.Token);
    Assert.Equal(nonce.Expose().ToArray(), received);
    await peer.GetStream().WriteAsync("OK\n"u8.ToArray(), timeout.Token);
    await using var client = await connecting;
    Assert.True(client.IsUsable);
  }

  [Fact]
  public async Task ExplicitUnixEndpointConsumesOnlyGreeting() {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new UnixFixture();
    await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(fixture.Socket()));
    var token = TestContext.Current.CancellationToken;
    var accepting = listener.AcceptAsync(token);
    var connecting = AssuanClient.ConnectAsync(listener.Endpoint, cancellationToken: token);
    await using var peer = (await accepting).Stream;
    await peer.WriteAsync("OK\n"u8.ToArray(), token);
    await using var client = await connecting;
    Assert.True(client.IsUsable);
    listener.Cleanup();
  }

  [Fact]
  public async Task ExplicitNamedPipeEndpointConsumesOnlyGreeting() {
    if (!OperatingSystem.IsWindows()) {
      return;
    }

    var endpoint = LocalEndpoint.NamedPipe(@"\\.\pipe\assuan-client-" + Guid.NewGuid().ToString("N"));
    await using var listener = PipeAcceptor.Bind(endpoint);
    var token = TestContext.Current.CancellationToken;
    var accepting = listener.AcceptAsync(token);
    var connecting = AssuanClient.ConnectAsync(endpoint, cancellationToken: token);
    await using var peer = (await accepting).Stream;
    await peer.WriteAsync("OK\n"u8.ToArray(), token);
    await using var client = await connecting;
    Assert.True(client.IsUsable);
  }
}
