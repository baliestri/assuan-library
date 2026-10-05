// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Unix;
using System.Net.Sockets;

namespace AssuanLibrary.Tests.Transport;

public sealed class UnixSecurityTests {
  [Fact]
  public async Task PrivateParentCreatesRestrictedSocketAndCleanupPreservesFutureReplacements() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
    using var fixture = new UnixFixture();
    var path = fixture.Socket();
    await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(path));
    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
      File.GetUnixFileMode(listener.Endpoint.Path) & (UnixFileMode)0x1ff);
    listener.Cleanup();
    Assert.False(UnixNative.TryInspectNoFollow(path, out _));
    listener.Cleanup();
    File.WriteAllText(path, "replacement");
    listener.Cleanup();
    Assert.Equal("replacement", File.ReadAllText(path));
  }

  [Fact]
  public void PermissiveOrSymlinkedParentAndExistingEndpointAreRejected() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
    using var fixture = new UnixFixture();
    var parent = Path.Combine(fixture.Root, "private");
    Directory.CreateDirectory(parent);
    File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite |
      UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
    Assert.Equal(TransportErrorKind.AccessPolicy, Assert.Throws<TransportException>(() =>
      UnixAcceptor.Bind(LocalEndpoint.Unix(Path.Combine(parent, "socket")))).Kind);
    File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    var link = Path.Combine(fixture.Root, "parent-link");
    Directory.CreateSymbolicLink(link, parent);
    Assert.Equal(TransportErrorKind.AccessPolicy, Assert.Throws<TransportException>(() =>
      UnixAcceptor.Bind(LocalEndpoint.Unix(Path.Combine(link, "socket")))).Kind);
    var occupied = Path.Combine(parent, "existing");
    File.WriteAllText(occupied, "keep");
    Assert.Throws<TransportException>(() => UnixAcceptor.Bind(LocalEndpoint.Unix(occupied)));
    Assert.Equal("keep", File.ReadAllText(occupied));
    var linkToTarget = Path.Combine(parent, "socket-link");
    File.CreateSymbolicLink(linkToTarget, occupied);
    Assert.Throws<TransportException>(() => UnixAcceptor.Bind(LocalEndpoint.Unix(linkToTarget)));
    Assert.Equal("keep", File.ReadAllText(occupied));
  }

  [Fact]
  public async Task CleanupRejectsReplacedSocketOrParentWithoutDeletingReplacement() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
    using var fixture = new UnixFixture();
    var parent = Path.Combine(fixture.Root, "parent");
    Directory.CreateDirectory(parent);
    File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    var path = Path.Combine(parent, "socket");
    await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(path));
    File.Delete(path);
    File.WriteAllText(path, "new owner");
    Assert.Equal(TransportErrorKind.AccessPolicy, Assert.Throws<TransportException>(listener.Cleanup).Kind);
    Assert.Equal("new owner", File.ReadAllText(path));
    File.Delete(path);
    Directory.Move(parent, Path.Combine(fixture.Root, "old-parent"));
    Directory.CreateDirectory(parent);
    File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    File.WriteAllText(path, "replacement parent");
    Assert.Equal(TransportErrorKind.AccessPolicy, Assert.Throws<TransportException>(listener.Cleanup).Kind);
    Assert.Equal("replacement parent", File.ReadAllText(path));
  }

  [Fact]
  public async Task DisposingListenerDoesNotUnlinkItsPath() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
    using var fixture = new UnixFixture();
    var path = fixture.Socket();
    var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(path));
    Assert.True(UnixNative.TryInspectNoFollow(path, out _), "Socket path missing immediately after bind.");
    await listener.DisposeAsync();
    Assert.True(UnixNative.TryInspectNoFollow(path, out _), "Socket path was removed when listener was disposed.");
  }

  [Fact]
  public async Task CleanupPreservesAReplacementSocketWithTheSamePath() {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
    using var fixture = new UnixFixture();
    var path = fixture.Socket();
    await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(path));
    File.Move(path, fixture.Socket("original-socket"));
    using var replacement = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    replacement.Bind(new UnixDomainSocketEndPoint(path));
    replacement.Listen();
    Assert.Equal(TransportErrorKind.AccessPolicy, Assert.Throws<TransportException>(listener.Cleanup).Kind);
    Assert.True(UnixNative.TryInspectNoFollow(path, out _));
  }
}
