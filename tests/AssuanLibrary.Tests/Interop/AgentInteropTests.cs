// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Tests.Interop;

public sealed class AgentInteropTests {
  [Fact]
  public void RealAgentCannotStartInAnUnisolatedLocalSession() {
    if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true") return;
    Assert.Throws<InvalidOperationException>(GnuPgTools.FromEnvironment);
  }

  [Fact]
  public void RequiredModeRejectsMissingOrUnrelatedTools() {
    Assert.Throws<InvalidOperationException>(() => GnuPgTools.Require(null, null, null));
    Assert.Throws<InvalidOperationException>(() => GnuPgTools.Require("relative/gpgconf", "relative/gpg-agent",
      "relative/gpg-connect-agent"));

    var root = Path.Combine(AssuanLibrary.Docs.DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory),
      "artifacts", "interop-config-tests", Guid.NewGuid().ToString("N"));
    var first = Path.Combine(root, "first");
    var second = Path.Combine(root, "second");
    Directory.CreateDirectory(first);
    Directory.CreateDirectory(second);
    try {
      var conf = Path.Combine(first, "gpgconf");
      var agent = Path.Combine(first, "gpg-agent");
      var peer = Path.Combine(second, "gpg-connect-agent");
      foreach (var path in new[] { conf, agent, peer }) File.WriteAllText(path, "fixture");
      Assert.Throws<InvalidOperationException>(() => GnuPgTools.Require(conf, agent, peer));
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public async Task IsolatedRealAgentAcceptsCommandsAndRemainsUsableAfterRemoteError() {
    if (!GnuPgFixture.Required) return;
    await using var fixture = new GnuPgFixture(GnuPgTools.FromEnvironment());
    await fixture.LaunchAgentAsync(TestContext.Current.CancellationToken);
    using var endpoint = await new AgentLocator(fixture.Tools.Gpgconf, fixture.Home)
      .ResolveAsync(TestContext.Current.CancellationToken);
    Assert.Equal(OperatingSystem.IsWindows() ? AgentEndpointKind.NativeWindowsAgent : AgentEndpointKind.UnixSocket,
      endpoint.Kind);
    await using var client = await AssuanClient.ConnectAsync(endpoint,
      cancellationToken: TestContext.Current.CancellationToken);
    var version = await client.CollectAsync("GETINFO", "version"u8.ToArray(),
      new CollectLimits(maxBytes: 128, maxEvents: 16), TestContext.Current.CancellationToken);
    Assert.True(version.Data.Length > 0, "The isolated agent returned no version data.");

    var rejected = await Assert.ThrowsAsync<ClientException>(() => client.CollectAsync(
      "NO_SUCH_COMMAND", ReadOnlyMemory<byte>.Empty, cancellationToken: TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Remote, rejected.Kind);
    await client.CollectAsync("NOP", ReadOnlyMemory<byte>.Empty,
      new CollectLimits(maxBytes: 128, maxEvents: 16), TestContext.Current.CancellationToken);
  }
}
