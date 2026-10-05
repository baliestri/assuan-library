// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace AssuanLibrary.Server;

/// <summary>Reports only redacted categories; never forwards exception or application text.</summary>
internal static class ServerDiagnostics {
  private static readonly DiagnosticListener _listener = new("AssuanLibrary.Server");

  internal static void Report(ServerErrorKind kind) {
    try {
      if (_listener.IsEnabled("SessionFailure")) {
        _listener.Write("SessionFailure", new { Kind = kind });
      }
    }
    catch {
      // A diagnostic observer must not affect permit release or session isolation.
    }
  }
}
