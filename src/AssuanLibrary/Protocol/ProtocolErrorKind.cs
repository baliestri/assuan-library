// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>A malformed wire field or framed line, without payload content.</summary>
public enum ProtocolErrorKind {
  /// <summary>A line contains NUL, CR, or LF outside its framing terminator.</summary>
  InvalidLine,

  /// <summary>A name or keyword violates the printable ASCII token grammar.</summary>
  InvalidToken,

  /// <summary>A server response category or separator is invalid.</summary>
  InvalidResponse,

  /// <summary>The unsigned decimal remote error code is missing or invalid.</summary>
  InvalidErrorCode,

  /// <summary>A percent escape is incomplete or not hexadecimal.</summary>
  InvalidEscape,

  /// <summary>A line exceeds the wire limit including LF or CRLF.</summary>
  LineTooLong,

  /// <summary>The stream ended before a complete framed line.</summary>
  UnexpectedEof
}
