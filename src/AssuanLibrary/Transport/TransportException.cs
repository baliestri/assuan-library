// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Transport;

/// <summary>A transport failure with a redacted message and separately accessible I/O cause.</summary>
/// <remarks>The cause may contain external text; the library does not include it in its own diagnostics.</remarks>
public sealed class TransportException : Exception {
  internal TransportException(TransportErrorKind kind, Exception? ioCause = null,
    ProtocolErrorKind? protocolCause = null) : base(kind switch {
    TransportErrorKind.Timeout => "transport deadline expired",
    TransportErrorKind.Cancelled => "transport operation cancelled",
    TransportErrorKind.Closed => "transport is closed",
    TransportErrorKind.AccessPolicy => "transport access policy failed",
    TransportErrorKind.InvalidEndpoint => "invalid transport endpoint",
    TransportErrorKind.InvalidOptions => "invalid transport options",
    TransportErrorKind.Io => "transport I/O failed",
    TransportErrorKind.Framing => "transport line framing failed",
    var _ => "transport failure"
  }) {
    Kind = kind;
    IoCause = ioCause;
    ProtocolCause = protocolCause;
  }

  /// <summary>The stable failure category.</summary>
  public TransportErrorKind Kind { get; }

  /// <summary>The original I/O cause, if any; inspect or log only after applying your own redaction policy.</summary>
  public Exception? IoCause { get; }

  /// <summary>The underlying framing category, when <see cref="Kind" /> is <see cref="TransportErrorKind.Framing" />.</summary>
  public ProtocolErrorKind? ProtocolCause { get; }
}
