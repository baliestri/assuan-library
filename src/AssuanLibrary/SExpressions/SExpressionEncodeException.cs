// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>An encoding error whose message never includes atom contents.</summary>
public sealed class SExpressionEncodeException : Exception {
  internal SExpressionEncodeException(SExpressionEncodeErrorKind kind) : base(kind switch {
    SExpressionEncodeErrorKind.SizeLimit => "canonical output byte limit exceeded",
    SExpressionEncodeErrorKind.AtomLimit => "atom byte limit exceeded",
    SExpressionEncodeErrorKind.NodeLimit => "expression node limit exceeded",
    SExpressionEncodeErrorKind.DepthLimit => "list nesting limit exceeded",
    SExpressionEncodeErrorKind.LengthOverflow => "canonical output length overflow",
    SExpressionEncodeErrorKind.AllocationFailed => "could not reserve encoding storage",
    var _ => "invalid canonical expression"
  }) => Kind = kind;

  /// <summary>The category of the encoding failure.</summary>
  public SExpressionEncodeErrorKind Kind { get; }
}
