// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>Category of a canonical S-expression encoding failure.</summary>
public enum SExpressionEncodeErrorKind {
  /// <summary>The resulting bytes would exceed the default input limit.</summary>
  SizeLimit,
  /// <summary>An atom exceeds the default payload limit.</summary>
  AtomLimit,
  /// <summary>The expression contains too many nodes.</summary>
  NodeLimit,
  /// <summary>List nesting exceeds the default depth limit.</summary>
  DepthLimit,
  /// <summary>Canonical length calculation overflowed.</summary>
  LengthOverflow,
  /// <summary>Output or traversal storage could not be reserved.</summary>
  AllocationFailed,
}

/// <summary>An encoding error whose message never includes atom contents.</summary>
public sealed class SExpressionEncodeException : Exception {
  /// <summary>The category of the encoding failure.</summary>
  public SExpressionEncodeErrorKind Kind { get; }

  internal SExpressionEncodeException(SExpressionEncodeErrorKind kind) : base(kind switch {
    SExpressionEncodeErrorKind.SizeLimit => "canonical output byte limit exceeded",
    SExpressionEncodeErrorKind.AtomLimit => "atom byte limit exceeded",
    SExpressionEncodeErrorKind.NodeLimit => "expression node limit exceeded",
    SExpressionEncodeErrorKind.DepthLimit => "list nesting limit exceeded",
    SExpressionEncodeErrorKind.LengthOverflow => "canonical output length overflow",
    SExpressionEncodeErrorKind.AllocationFailed => "could not reserve encoding storage",
    _ => "invalid canonical expression",
  }) => Kind = kind;
}
