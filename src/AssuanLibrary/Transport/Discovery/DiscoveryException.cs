// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Category of an agent-discovery failure without paths, output, or nonce bytes.</summary>
public enum DiscoveryErrorKind {
  /// <summary>The selected executable failed or exited unsuccessfully.</summary>
  ProcessFailed,

  /// <summary>The selected process or native socket file returned invalid data.</summary>
  InvalidOutput,

  /// <summary>Combined stdout and stderr exceeded the fixed byte budget.</summary>
  OutputLimit,

  /// <summary>The socket file uses a format that this library does not support.</summary>
  UnsupportedFormat,

  /// <summary>An external process, file or socket operation failed.</summary>
  Io,

  /// <summary>The operation exceeded its total deadline.</summary>
  Timeout,

  /// <summary>The caller canceled the operation.</summary>
  Cancelled
}

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
