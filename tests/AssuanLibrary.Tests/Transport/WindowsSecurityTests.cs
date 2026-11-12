// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Windows;
using Microsoft.Win32.SafeHandles;

namespace AssuanLibrary.Tests.Transport;

public sealed class WindowsSecurityTests {
  [Fact]
  public void AProtectedDaclContainsOnlyTheCurrentProcessUser() {
    if (!OperatingSystem.IsWindows()) {
      return;
    }

    using var current = WindowsIdentity.GetCurrent();
    var sid = Assert.IsType<SecurityIdentifier>(current.User);
    var policy = PipeSecurityFactory.ForCurrentUser();
    Assert.True(policy.AreAccessRulesProtected);
    Assert.Equal(sid, policy.GetOwner(typeof(SecurityIdentifier)));
    var rules = policy.GetAccessRules(true, true, typeof(SecurityIdentifier));
    var only = Assert.Single(rules.Cast<PipeAccessRule>());
    Assert.Equal(sid, only.IdentityReference);
    Assert.Equal(AccessControlType.Allow, only.AccessControlType);
    Assert.Equal(PipeAccessRights.FullControl, only.PipeAccessRights);
  }

  [Fact]
  public async Task ASecondListenerCannotReuseOrDisturbAnExistingPipeName() {
    if (!OperatingSystem.IsWindows()) {
      return;
    }

    var endpoint = LocalEndpoint.NamedPipe($@"\\.\pipe\assuan-test-{Guid.NewGuid():N}");
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
    await using var client =
      await PipeConnector.ConnectAsync(endpoint, cancellationToken: TestContext.Current.CancellationToken);
    var admitted = await accepting;
    await using var accepted = admitted.Stream;
    Assert.Null(admitted.PeerIdentity);
  }

  [Fact]
  public async Task AConnectedClientCannotBeImpersonatedByThePipeServer() {
    if (!OperatingSystem.IsWindows()) {
      return;
    }

    var name = $"assuan-test-{Guid.NewGuid():N}";
    var token = TestContext.Current.CancellationToken;
    await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
      PipeOptions.Asynchronous);
    var waiting = server.WaitForConnectionAsync(token);
    await using var client = await PipeConnector.ConnectAsync(LocalEndpoint.NamedPipe($@"\\.\pipe\{name}"),
      cancellationToken: token);
    await waiting;
    // A server may only impersonate after it has read from the pipe. This pipe is unbuffered,
    // so the read must already be pending for the client's write to complete.
    var received = new byte[1];
    var reading = server.ReadExactlyAsync(received, token);
    await client.WriteAsync(new byte[] { 1 }, token);
    await reading;
    Assert.Equal(TokenImpersonationLevel.Identification, ImpersonationLevelGrantedTo(server));
  }

  // RunAsClient would execute managed code while impersonating; at identification level that code
  // cannot open anything. Take the thread token natively, revert at once, and inspect it afterwards.
  [SupportedOSPlatform("windows")]
  private static TokenImpersonationLevel ImpersonationLevelGrantedTo(NamedPipeServerStream server) {
    const uint TOKEN_DUPLICATE_AND_QUERY = 0x0002 | 0x0008;
    // Resolve both entry points before the thread carries the client's restricted token.
    _ = OpenThreadToken(GetCurrentThread(), TOKEN_DUPLICATE_AND_QUERY, true, out var none);
    none.Dispose();
    _ = RevertToSelf();
    var pipe = server.SafePipeHandle;
    var thread = GetCurrentThread();
    // No managed call may run between impersonating and reverting.
    var impersonated = ImpersonateNamedPipeClient(pipe);
    var opened = OpenThreadToken(thread, TOKEN_DUPLICATE_AND_QUERY, true, out var token);
    var reverted = RevertToSelf();
    using (token) {
      Assert.True(reverted);
      Assert.True(impersonated);
      Assert.True(opened);
      using var identity = new WindowsIdentity(token.DangerousGetHandle());
      return identity.ImpersonationLevel;
    }
  }

  [DllImport("advapi32.dll", SetLastError = true)]
  [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private extern static bool ImpersonateNamedPipeClient(SafePipeHandle pipe);

  [DllImport("advapi32.dll", SetLastError = true)]
  [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private extern static bool RevertToSelf();

  [DllImport("advapi32.dll", SetLastError = true)]
  [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private extern static bool OpenThreadToken(IntPtr thread, uint access,
    [MarshalAs(UnmanagedType.Bool)] bool openAsSelf, out SafeAccessTokenHandle token);

  [DllImport("kernel32.dll")]
  [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
  private extern static IntPtr GetCurrentThread();

  [Fact]
  public void OnlyTheCurrentUserAndTheLocalSystemAreTrustedPipeOwners() {
    if (!OperatingSystem.IsWindows()) {
      return;
    }

    using var current = WindowsIdentity.GetCurrent();
    var user = Assert.IsType<SecurityIdentifier>(current.User);
    Assert.True(PipeSecurityFactory.IsTrustedOwner(user, user));
    Assert.True(PipeSecurityFactory.IsTrustedOwner(
      new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), user));
    Assert.True(PipeSecurityFactory.IsTrustedOwner(
      new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), user));
    Assert.False(PipeSecurityFactory.IsTrustedOwner(new SecurityIdentifier(WellKnownSidType.WorldSid, null), user));
    Assert.False(PipeSecurityFactory.IsTrustedOwner(
      new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), user));
    Assert.False(PipeSecurityFactory.IsTrustedOwner(null, user));
    Assert.False(PipeSecurityFactory.IsTrustedOwner(user, null));
  }
}
