// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>Parses exactly one canonical binary S-expression without copying atom data.</summary>
public static class SExpressionParser {
  /// <summary>Parses all input bytes and rejects trailing data, including whitespace.</summary>
  /// <remarks>The caller must keep the supplied bytes stable while using the returned view.</remarks>
  /// <exception cref="SExpressionParseException">The input is invalid or exceeds the limits.</exception>
  public static SExpressionView ParseComplete(ReadOnlySpan<byte> input, SExpressionLimits? limits = null) {
    var view = Parse(input, limits ?? SExpressionLimits.Default, out var consumed);
    if (consumed != input.Length) {
      throw new SExpressionParseException(SExpressionParseErrorKind.TrailingData, consumed);
    }

    return view;
  }

  /// <summary>Parses one expression; the input limit includes the unexamined suffix.</summary>
  /// <remarks>The caller must keep the supplied bytes stable while using the returned view.</remarks>
  /// <exception cref="SExpressionParseException">The prefix is invalid or the entire input exceeds the limits.</exception>
  public static SExpressionView ParsePrefix(ReadOnlySpan<byte> input, out int bytesConsumed)
    => ParsePrefix(input, SExpressionLimits.Default, out bytesConsumed);

  /// <summary>Parses one expression with explicit limits; the suffix remains unexamined.</summary>
  /// <exception cref="SExpressionParseException">The prefix is invalid or the entire input exceeds the limits.</exception>
  public static SExpressionView ParsePrefix(ReadOnlySpan<byte> input, SExpressionLimits limits, out int bytesConsumed) {
    ArgumentNullException.ThrowIfNull(limits);
    return Parse(input, limits, out bytesConsumed);
  }

  private static SExpressionView Parse(ReadOnlySpan<byte> input, SExpressionLimits limits, out int bytesConsumed) {
    if (input.Length > limits.MaxInputBytes) {
      throw new SExpressionParseException(SExpressionParseErrorKind.InputLimit, limits.MaxInputBytes);
    }

    var end = SExpressionScanner.Scan(input, limits, 0);
    bytesConsumed = end;
    return new SExpressionView(input[..end]);
  }
}
