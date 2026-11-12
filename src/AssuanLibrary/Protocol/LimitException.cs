// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>An error from a bounded buffer; never formats stored bytes.</summary>
public sealed class LimitException : Exception {
  internal LimitException(LimitErrorKind kind) : base(kind switch {
    LimitErrorKind.AllocationFailed => "storage allocation failed",
    LimitErrorKind.LengthOverflow => "buffer length overflow",
    LimitErrorKind.CapacityExceeded => "buffer capacity exceeded",
    var _ => "buffer limit exceeded"
  }) => Kind = kind;

  /// <summary>The category of limit exceeded.</summary>
  public LimitErrorKind Kind { get; }
}
