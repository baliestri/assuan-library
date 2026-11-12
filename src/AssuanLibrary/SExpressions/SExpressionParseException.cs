// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;

namespace AssuanLibrary.SExpressions;

/// <summary>A parsing error with an optional zero-based byte offset, never including the input bytes.</summary>
public sealed class SExpressionParseException : Exception {
  internal SExpressionParseException(SExpressionParseErrorKind kind, int? offset = null)
    : base(offset is { } index
      ? string.Create(CultureInfo.InvariantCulture, $"{Describe(kind)} at byte {index}")
      : Describe(kind)) {
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
