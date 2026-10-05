// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
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
    catch (Exception error) when (error is UnauthorizedAccessException or SecurityException or InvalidOperationException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }
  }
}
