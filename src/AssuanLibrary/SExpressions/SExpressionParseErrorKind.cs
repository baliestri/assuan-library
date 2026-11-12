// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>Category of a canonical S-expression parsing failure.</summary>
public enum SExpressionParseErrorKind {
  /// <summary>The configured resource limits are invalid.</summary>
  InvalidLimits,

  /// <summary>The entire input exceeds its byte limit.</summary>
  InputLimit,

  /// <summary>An atom declares more payload bytes than allowed.</summary>
  AtomLimit,

  /// <summary>The expression contains too many atoms and lists.</summary>
  NodeLimit,

  /// <summary>The number of nested lists exceeds the limit.</summary>
  DepthLimit,

  /// <summary>An atom's decimal length overflows a 64-bit unsigned integer.</summary>
  LengthOverflow,

  /// <summary>The decimal length is not canonical, such as a leading zero.</summary>
  InvalidLength,

  /// <summary>The length has no following colon.</summary>
  ExpectedColon,

  /// <summary>A byte cannot begin an expression at this position.</summary>
  UnexpectedToken,

  /// <summary>The input ends before the expression finishes.</summary>
  UnexpectedEof,

  /// <summary>Bytes follow a complete expression.</summary>
  TrailingData,

  /// <summary>Storage could not be reserved for the expression.</summary>
  AllocationFailed
}
