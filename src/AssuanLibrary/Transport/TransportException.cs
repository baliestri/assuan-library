// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Transport;

/// <summary>The category of a transport failure without bytes or external error text.</summary>
public enum TransportErrorKind {
  /// <summary>The total operation deadline expired.</summary>
  Timeout,

  /// <summary>The caller canceled the operation.</summary>
  Cancelled,

  /// <summary>The channel or listener was closed.</summary>
  Closed,

  /// <summary>The required local access policy could not be established or verified.</summary>
  AccessPolicy,

  /// <summary>The requested address is invalid for this transport.</summary>
  InvalidEndpoint,

  /// <summary>The requested timeout or other option is unsupported.</summary>
  InvalidOptions,

  /// <summary>The operating system or underlying stream reported an I/O failure.</summary>
  Io,

  /// <summary>Wire-line framing failed.</summary>
  Framing
}

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
