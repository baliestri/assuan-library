// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AssuanLibrary.Transport.Windows;

internal static class PipeSecurityFactory {
  internal static PipeSecurity ForCurrentUser() {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    try {
      using var identity = WindowsIdentity.GetCurrent();
      var sid = identity.User ?? throw new TransportException(TransportErrorKind.AccessPolicy);
      var security = new PipeSecurity();
      security.SetOwner(sid);
      security.SetAccessRuleProtection(true, false);
      security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
      return security;
    }
    catch (TransportException) {
      throw;
    }
    catch (Exception error) when
      (error is UnauthorizedAccessException or SecurityException or InvalidOperationException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }
  }

  // A pipe name is first come, first served: another account could create it before the intended
  // server does. Refuse a pipe that is not owned by this user or by the local system itself.
  internal static void RequireTrustedServerOwner(PipeStream connected) {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    try {
      using var identity = WindowsIdentity.GetCurrent();
      var owner = connected.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
      if (!IsTrustedOwner(owner, identity.User)) {
        throw new TransportException(TransportErrorKind.AccessPolicy);
      }
    }
    catch (TransportException) {
      throw;
    }
    catch (Exception error) when
      (error is UnauthorizedAccessException or SecurityException or InvalidOperationException or IOException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }
  }

  // An elevated process owns its objects as the Administrators group, and services as LocalSystem;
  // both already control this machine, so they are not the impostor this check is about.
  internal static bool IsTrustedOwner(SecurityIdentifier? owner, SecurityIdentifier? currentUser) {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    return owner is not null &&
           currentUser is not null &&
           (owner.Equals(currentUser) ||
            owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
            owner.IsWellKnown(WellKnownSidType.LocalSystemSid));
  }
}
