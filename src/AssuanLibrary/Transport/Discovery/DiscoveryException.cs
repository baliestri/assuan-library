// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Discovery;

/// <summary>An agent-discovery failure whose own diagnostics never include external data.</summary>
public sealed class DiscoveryException : Exception {
  internal DiscoveryException(DiscoveryErrorKind kind, Exception? ioCause = null) : base(kind switch {
    DiscoveryErrorKind.ProcessFailed => "agent discovery process failed",
    DiscoveryErrorKind.InvalidOutput => "invalid agent discovery output",
    DiscoveryErrorKind.OutputLimit => "agent discovery output limit exceeded",
    DiscoveryErrorKind.UnsupportedFormat => "unsupported agent socket format",
    DiscoveryErrorKind.Io => "agent discovery I/O failed",
    DiscoveryErrorKind.Timeout => "agent discovery deadline expired",
    DiscoveryErrorKind.Cancelled => "agent discovery cancelled",
    var _ => "agent discovery failed"
  }) {
    Kind = kind;
    IoCause = ioCause;
  }

  /// <summary>The failure category.</summary>
  public DiscoveryErrorKind Kind { get; }

  /// <summary>The original I/O cause, if any; the caller is responsible for redacting it before logging.</summary>
  public Exception? IoCause { get; }
}
