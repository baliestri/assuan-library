// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
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
  AllocationFailed
}
