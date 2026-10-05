// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Windows;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AssuanLibrary.Tests.Transport;

public sealed class WindowsSecurityTests {
  [Fact]
  public void AProtectedDaclContainsOnlyTheCurrentProcessUser() {
    if (!OperatingSystem.IsWindows()) return;
    using var current = WindowsIdentity.GetCurrent();
    var sid = Assert.IsType<SecurityIdentifier>(current.User);
    var policy = PipeSecurityFactory.ForCurrentUser();
    Assert.True(policy.AreAccessRulesProtected);
    Assert.Equal(sid, policy.GetOwner(typeof(SecurityIdentifier)));
    var rules = policy.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
    var only = Assert.Single(rules.Cast<PipeAccessRule>());
    Assert.Equal(sid, only.IdentityReference);
    Assert.Equal(AccessControlType.Allow, only.AccessControlType);
    Assert.Equal(PipeAccessRights.FullControl, only.PipeAccessRights);
  }

  [Fact]
  public async Task ASecondListenerCannotReuseOrDisturbAnExistingPipeName() {
    if (!OperatingSystem.IsWindows()) return;
    var endpoint = LocalEndpoint.NamedPipe(@"\\.\pipe\assuan-test-" + Guid.NewGuid().ToString("N"));
    await using var first = PipeAcceptor.Bind(endpoint);
    var waiting = Assert.IsType<NamedPipeServerStream>(typeof(PipeAcceptor)
      .GetField("_waiting", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(first));
    var applied = waiting.GetAccessControl();
    Assert.True(applied.AreAccessRulesProtected);
    using var current = WindowsIdentity.GetCurrent();
    Assert.Equal(current.User, applied.GetOwner(typeof(SecurityIdentifier)));
    Assert.Single(applied.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>());
    var error = Assert.Throws<TransportException>(() => PipeAcceptor.Bind(endpoint));
    Assert.Equal(TransportErrorKind.Io, error.Kind);
    var accepting = first.AcceptAsync(TestContext.Current.CancellationToken);
    using var client = await PipeConnector.ConnectAsync(endpoint, cancellationToken: TestContext.Current.CancellationToken);
    var admitted = await accepting;
    using var accepted = admitted.Stream;
    Assert.Null(admitted.PeerIdentity);
  }
}
