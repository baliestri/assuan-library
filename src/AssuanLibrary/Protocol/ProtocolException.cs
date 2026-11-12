// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>A protocol error whose message never includes wire bytes.</summary>
public sealed class ProtocolException : Exception {
  internal ProtocolException(ProtocolErrorKind kind) : base(kind switch {
    ProtocolErrorKind.InvalidLine => "invalid byte in protocol line",
    ProtocolErrorKind.InvalidToken => "invalid protocol token",
    ProtocolErrorKind.InvalidResponse => "invalid server response",
    ProtocolErrorKind.InvalidErrorCode => "invalid remote error code",
    ProtocolErrorKind.InvalidEscape => "invalid percent escape",
    ProtocolErrorKind.LineTooLong => "protocol line exceeds wire limit",
    ProtocolErrorKind.UnexpectedEof => "incomplete protocol line at EOF",
    var _ => "protocol failure"
  }) => Kind = kind;

  /// <summary>The category of failure.</summary>
  public ProtocolErrorKind Kind { get; }
}
