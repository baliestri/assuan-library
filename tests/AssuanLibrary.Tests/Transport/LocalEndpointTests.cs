// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Transport;

public sealed class LocalEndpointTests {
  [Fact]
  public void ValidatesNamedPipePathBeforeOpeningAnything() {
    if (!OperatingSystem.IsWindows()) {
      Assert.Throws<PlatformNotSupportedException>(() => LocalEndpoint.NamedPipe(@"\\.\pipe\test"));
      return;
    }

    var valid = LocalEndpoint.NamedPipe(@"\\.\pipe\assuan-test");
    Assert.Equal(LocalEndpointKind.NamedPipe, valid.Kind);
    Assert.Equal(@"\\.\pipe\assuan-test", valid.Path);
    Assert.DoesNotContain("assuan-test", valid.ToString(), StringComparison.Ordinal);
    foreach (var path in new[] {
               @"\\remote\pipe\test", @"\\.\pipe\", @"C:\test",
               "\\\\.\\pipe\\bad\0name", @"\\.\pipe\nested\name", @"\\.\pipe\nested/name"
             })
      Assert.Equal(TransportErrorKind.InvalidEndpoint,
        Assert.Throws<TransportException>(() => LocalEndpoint.NamedPipe(path)).Kind);

    const string PREFIX = @"\\.\pipe\";
    Assert.Equal(256, LocalEndpoint.NamedPipe(PREFIX + new string('x', 256 - PREFIX.Length)).Path.Length);
    Assert.Throws<TransportException>(() => LocalEndpoint.NamedPipe(PREFIX + new string('x', 257 - PREFIX.Length)));
  }

  [Fact]
  public void UnixPathsAreExplicitWithoutImposingListenerPolicyOnClients() {
    if (OperatingSystem.IsWindows()) {
      Assert.Throws<PlatformNotSupportedException>(() => LocalEndpoint.Unix("/socket"));
      return;
    }

    var endpoint = LocalEndpoint.Unix("/tmp/external-agent.sock");
    Assert.Equal(LocalEndpointKind.UnixSocket, endpoint.Kind);
    Assert.Equal("/tmp/external-agent.sock", endpoint.Path);
    Assert.DoesNotContain("external-agent", endpoint.ToString(), StringComparison.Ordinal);
    Assert.Equal(" ", LocalEndpoint.Unix(" ").Path); // Literal Unix filenames are not trimmed.
    Assert.Throws<TransportException>(() => LocalEndpoint.Unix(string.Empty));
    Assert.Throws<TransportException>(() => LocalEndpoint.Unix("/bad\0path"));
  }
}
