// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using System.Text;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;
using AssuanLibrary.Transport.Unix;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.Transport;

public sealed class UnixAgentPathTests {
  [Fact]
  public async Task LiteralPercentPathConnectsWithoutDecodingOrConsumingGreeting() {
    if (OperatingSystem.IsWindows()) {
      return;
    }

    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new AgentFixture();
    File.SetUnixFileMode(fixture.Home, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(fixture.SocketFile));
    var token = TestContext.Current.CancellationToken;
    using var resolved = await new AgentLocator(fixture.Executable, fixture.Home).ResolveAsync(token);
    Assert.Equal(AgentEndpointKind.UnixSocket, resolved.Kind);
    Assert.True(resolved.UnixPathBytes.EndsWith("socket %20 literal"u8));
    var accepting = listener.AcceptAsync(token);
    await using var client = await resolved.ConnectAsync(TimeSpan.FromSeconds(2), token);
    await using var peer = (await accepting).Stream;
    await peer.WriteAsync("OK\n"u8.ToArray().AsMemory(), token);
    var greeting = new byte[3];
    await client.ReadExactlyAsync(greeting, token);
    Assert.Equal("OK\n"u8.ToArray(), greeting);
    listener.Cleanup();
  }

  [Fact]
  public async Task InvalidUtf8PathBytesReachTheExactRawUnixSocket() {
    if (OperatingSystem.IsWindows()) {
      return;
    }

    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new AgentFixture();
    byte[] rawPath = [
      .. Encoding.UTF8.GetBytes(fixture.Home + "/"),
      (byte)'s', (byte)'o', (byte)'c', (byte)'k', (byte)'e', (byte)'t', 0xff, (byte)'%', (byte)'2', (byte)'0'
    ];
    File.WriteAllBytes(Path.Combine(fixture.Home, "output.bin"), [.. rawPath, (byte)'\n']);
    var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    UnixNative.BindRawSocket(listener, rawPath);
    try {
      listener.Listen();
      listener.Blocking = false;
      var token = TestContext.Current.CancellationToken;
      var resolved = await new AgentLocator(fixture.Executable, fixture.Home).ResolveAsync(token);
      Assert.Equal(rawPath, resolved.UnixPathBytes.ToArray());
      await using var client = await resolved.ConnectAsync(TimeSpan.FromSeconds(2), token);
      Socket? peer = null;
      for (var attempt = 0; peer is null && attempt < 100; ++attempt) {
        peer = UnixNative.TryAccept(listener);
        if (peer is null) {
          await Task.Delay(TimeSpan.FromMilliseconds(10), token);
        }
      }

      using (var connectedPeer = peer ?? throw new XunitException("Raw socket did not accept the client.")) {
        await client.WriteAsync(new byte[] { 0xff }.AsMemory(), token);
        var data = new byte[1];
        Assert.Equal(1, await connectedPeer.ReceiveAsync(data.AsMemory(), SocketFlags.None, token));
        Assert.Equal((byte)0xff, data[0]);
        await connectedPeer.SendAsync("OK\n"u8.ToArray().AsMemory(), SocketFlags.None, token);
        var response = new byte[3];
        await client.ReadExactlyAsync(response, token);
        Assert.Equal("OK\n"u8.ToArray(), response);
      }

      resolved.Dispose();
      await Assert.ThrowsAsync<ObjectDisposedException>(() => resolved.ConnectAsync(cancellationToken: token));
    }
    finally {
      listener.Dispose();
      UnixNative.UnlinkRawFixturePath(rawPath);
    }
  }
}
