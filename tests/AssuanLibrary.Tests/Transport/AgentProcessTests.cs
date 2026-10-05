// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Tests.Transport;

public sealed class AgentProcessTests {
  [Fact]
  public async Task LocatorResolvesThePlatformEndpointWithoutLaunchingAnAgent() {
    using var fixture = new AgentFixture();
    if (OperatingSystem.IsWindows()) {
      File.WriteAllBytes(fixture.SocketFile,
        [.. "32123\n"u8.ToArray(), .. Enumerable.Range(0, 16).Select(value => (byte)value)]);
    }

    var resolved = await new AgentLocator(fixture.Executable, fixture.Home)
      .ResolveAsync(TestContext.Current.CancellationToken);
    if (OperatingSystem.IsWindows()) {
      Assert.Equal(AgentEndpointKind.NativeWindowsAgent, resolved.Kind);
      Assert.Equal(32123, resolved.TcpEndpoint!.Port);
      Assert.Throws<InvalidOperationException>(() => resolved.UnixPathBytes.ToArray());
    }
    else {
      Assert.Equal(AgentEndpointKind.UnixSocket, resolved.Kind);
      Assert.Equal(Encoding.UTF8.GetBytes(fixture.SocketFile), resolved.UnixPathBytes.ToArray());
    }

    Assert.DoesNotContain(fixture.Home, resolved.ToString());
    resolved.Dispose();
    Assert.Throws<ObjectDisposedException>(() => _ = resolved.Kind);
  }

  [Fact]
  public async Task LocatorRejectsMalformedOutputAfterASuccessfulProcess() {
    using var fixture = new AgentFixture(output: "relative\n"u8.ToArray());
    var error = await Assert.ThrowsAsync<DiscoveryException>(() =>
      new AgentLocator(fixture.Executable, fixture.Home).ResolveAsync(TestContext.Current.CancellationToken));
    Assert.Equal(DiscoveryErrorKind.InvalidOutput, error.Kind);
    Assert.DoesNotContain(fixture.Home, error.ToString());
  }

  [Fact]
  public async Task ExplicitArgumentsPreserveSpacesAndPercentSequences() {
    using var fixture = new AgentFixture();
    using var output = await GpgconfRunner.RunAsync(fixture.Executable, fixture.Home,
      new TransportDeadline(TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);
    Assert.Equal(Encoding.UTF8.GetBytes(fixture.SocketFile + "\n"), output.Expose().ToArray());
    Assert.Equal(new[] { "--homedir", fixture.Home, "--list-dirs", "agent-socket" },
      File.ReadAllLines(Path.Combine(fixture.Home, "arguments.txt")));
  }

  [Theory]
  [InlineData("stderr")]
  [InlineData("aggregate")]
  public async Task StdoutAndStderrShareOneMiBBudget(string mode) {
    using var fixture = new AgentFixture(mode);
    var error = await Assert.ThrowsAsync<DiscoveryException>(() => GpgconfRunner.RunAsync(
      fixture.Executable, fixture.Home, new TransportDeadline(TimeSpan.FromSeconds(5)),
      TestContext.Current.CancellationToken));
    Assert.Equal(DiscoveryErrorKind.OutputLimit, error.Kind);
  }

  [Fact]
  public async Task NonzeroExitIsNotInterpretedAsAnEndpoint() {
    using var fixture = new AgentFixture("failure");
    var error = await Assert.ThrowsAsync<DiscoveryException>(() => GpgconfRunner.RunAsync(
      fixture.Executable, fixture.Home, new TransportDeadline(TimeSpan.FromSeconds(5)),
      TestContext.Current.CancellationToken));
    Assert.Equal(DiscoveryErrorKind.ProcessFailed, error.Kind);
    Assert.DoesNotContain(fixture.Home, error.ToString());
  }

  [Fact]
  public async Task TimeoutTerminatesAndWaitsForTheSelectedProcess() {
    using var fixture = new AgentFixture("timeout");
    var error = await Assert.ThrowsAsync<DiscoveryException>(() => GpgconfRunner.RunAsync(
      fixture.Executable, fixture.Home, new TransportDeadline(TimeSpan.FromMilliseconds(500)),
      TestContext.Current.CancellationToken));
    Assert.Equal(DiscoveryErrorKind.Timeout, error.Kind);
    Assert.False(File.Exists(Path.Combine(fixture.Home, "completed.txt")));
    var pidFile = Path.Combine(fixture.Home, "pid.txt");
    Assert.True(File.Exists(pidFile));
    var pid = int.Parse(File.ReadAllText(pidFile), CultureInfo.InvariantCulture);
    try {
      using var child = Process.GetProcessById(pid);
      Assert.True(child.HasExited);
    }
    catch (ArgumentException) {
      /* A reaped process has no process table entry. */
    }
  }

  [Fact]
  public async Task MissingExecutableReturnsTypedIoWithoutFormattingPath() {
    using var fixture = new AgentFixture();
    var missing = Path.Combine(fixture.Home, "no-such-gpgconf");
    var error = await Assert.ThrowsAsync<DiscoveryException>(() => GpgconfRunner.RunAsync(
      missing, fixture.Home, new TransportDeadline(TimeSpan.FromSeconds(5)),
      TestContext.Current.CancellationToken));
    Assert.Equal(DiscoveryErrorKind.Io, error.Kind);
    Assert.DoesNotContain(missing, error.ToString());
    Assert.NotNull(error.IoCause);
  }
}
