// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Unix;

namespace AssuanLibrary.Tests.Transport;

public sealed class UnixSocketTests {
  [Fact]
  public async Task NonAsciiDirectoryAndSocketNamesReachNativeUnixCallsAsUtf8() {
    if (OperatingSystem.IsWindows()) {
      return;
    }

    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new UnixFixture();
    var parent = Path.Combine(fixture.Root, "privé-漢");
    Directory.CreateDirectory(parent);
    File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    var path = Path.Combine(parent, "söcket-名%20");
    var endpoint = LocalEndpoint.Unix(path);
    Assert.Equal(path, endpoint.Path);
    await using var listener = UnixAcceptor.Bind(endpoint);
    var token = TestContext.Current.CancellationToken;
    var accepting = listener.AcceptAsync(token);
    await using var client = await UnixConnector.ConnectAsync(listener.Endpoint, cancellationToken: token);
    var admitted = await accepting;
    await using var peer = admitted.Stream;
    Assert.Equal(UnixNative.EffectiveUid, Assert.IsType<UnixPeerIdentity>(admitted.PeerIdentity).Uid);
    await client.WriteAsync(new byte[] { 0, 0xff }.AsMemory(), token);
    var bytes = new byte[2];
    await peer.ReadExactlyAsync(bytes, token);
    Assert.Equal(new byte[] { 0, 0xff }, bytes);
    listener.Cleanup();
    Assert.False(UnixNative.TryInspectNoFollow(path, out var _));
  }

  [Fact]
  public async Task VerifiesPeerCredentialsAndTransfersBinaryData() {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new UnixFixture();
    await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(fixture.Socket()));
    var token = TestContext.Current.CancellationToken;
    var accepting = listener.AcceptAsync(token);
    await using var client = await UnixConnector.ConnectAsync(listener.Endpoint, cancellationToken: token);
    var admitted = await accepting;
    await using var peer = admitted.Stream;
    var credentials = Assert.IsType<UnixPeerIdentity>(admitted.PeerIdentity);
    Assert.Equal(UnixNative.EffectiveUid, credentials.Uid);
    if (OperatingSystem.IsLinux()) {
      Assert.Equal((uint)Environment.ProcessId, credentials.Pid);
    }

    await client.WriteAsync(new byte[] { 0, 0xff, (byte)'\r', (byte)'\n' }.AsMemory(), token);
    var bytes = new byte[4];
    await peer.ReadExactlyAsync(bytes, token);
    Assert.Equal(new byte[] { 0, 0xff, (byte)'\r', (byte)'\n' }, bytes);
    listener.Cleanup();
  }

  [Fact]
  public async Task OutgoingConnectorDoesNotDemandPrivateParentOnAnExternalService() {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new UnixFixture();
    var parent = Path.Combine(fixture.Root, "external");
    Directory.CreateDirectory(parent);
    File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                 UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
    var endpoint = LocalEndpoint.Unix(Path.Combine(parent, "socket"));
    using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    listener.Bind(new UnixDomainSocketEndPoint(endpoint.Path));
    listener.Listen();
    var token = TestContext.Current.CancellationToken;
    var accepting = listener.AcceptAsync(token);
    await using var client = await UnixConnector.ConnectAsync(endpoint, cancellationToken: token);
    using var peer = await accepting;
    Assert.True(client.CanRead);
  }

  [Fact]
  public async Task CancelledAcceptLeavesUnixListenerUsable() {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    using var fixture = new UnixFixture();
    await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(fixture.Socket()));
    using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    cancelled.Cancel();
    var error = await Assert.ThrowsAsync<TransportException>(async () => await listener.AcceptAsync(cancelled.Token));
    Assert.Equal(TransportErrorKind.Cancelled, error.Kind);
    var pending = listener.AcceptAsync(TestContext.Current.CancellationToken);
    await using var client = await UnixConnector.ConnectAsync(listener.Endpoint,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var peer = (await pending).Stream;
    listener.Cleanup();
  }

  [Fact]
  public void CredentialValidationRejectsMismatchEvenWhenExpectedUidIsRoot() {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      return;
    }

    Assert.Equal(TransportErrorKind.AccessPolicy,
      Assert.Throws<TransportException>(() => UnixNative.AuthorizeUid(1, 0)).Kind);
    Assert.Equal(TransportErrorKind.AccessPolicy,
      Assert.Throws<TransportException>(() => UnixNative.AuthorizeUid(0, 1)).Kind);
    UnixNative.AuthorizeUid(42, 42);
  }
}
