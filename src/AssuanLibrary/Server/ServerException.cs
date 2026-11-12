// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>A server-session error without command or payload bytes in its diagnostics.</summary>
public sealed class ServerException : Exception {
  internal ServerException(ServerErrorKind kind, Exception? technicalCause = null)
    : base($"server session failed: {kind}") {
    Kind = kind;
    TechnicalCause = technicalCause;
  }

  /// <summary>The failure category.</summary>
  public ServerErrorKind Kind { get; }

  /// <summary>Optional external cause; redact its text before displaying or logging it.</summary>
  public Exception? TechnicalCause { get; }
}
