// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
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

/// <summary>A parsing error with an optional zero-based byte offset, never including the input bytes.</summary>
public sealed class SExpressionParseException : Exception {
  internal SExpressionParseException(SExpressionParseErrorKind kind, int? offset = null)
    : base(offset is { } index ? $"{Describe(kind)} at byte {index}" : Describe(kind)) {
    Kind = kind;
    Offset = offset;
  }

  /// <summary>The category of the parsing failure.</summary>
  public SExpressionParseErrorKind Kind { get; }

  /// <summary>The zero-based byte offset, or null when limits are invalid.</summary>
  public int? Offset { get; }

  private static string Describe(SExpressionParseErrorKind kind) => kind switch {
    SExpressionParseErrorKind.InvalidLimits => "invalid S-expression limits",
    SExpressionParseErrorKind.InputLimit => "input byte limit exceeded",
    SExpressionParseErrorKind.AtomLimit => "atom byte limit exceeded",
    SExpressionParseErrorKind.NodeLimit => "expression node limit exceeded",
    SExpressionParseErrorKind.DepthLimit => "list nesting limit exceeded",
    SExpressionParseErrorKind.LengthOverflow => "atom length overflow",
    SExpressionParseErrorKind.InvalidLength => "non-canonical atom length",
    SExpressionParseErrorKind.ExpectedColon => "expected colon after atom length",
    SExpressionParseErrorKind.UnexpectedToken => "unexpected expression byte",
    SExpressionParseErrorKind.UnexpectedEof => "incomplete S-expression",
    SExpressionParseErrorKind.TrailingData => "data after complete S-expression",
    SExpressionParseErrorKind.AllocationFailed => "could not reserve expression storage",
    var _ => "invalid S-expression"
  };
}
