// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>A fixed-capacity or arithmetic limit failure, without payload content.</summary>
public enum LimitErrorKind {
  /// <summary>Storage could not be reserved by the allocator.</summary>
  AllocationFailed,

  /// <summary>Combining lengths exceeded their numeric representation.</summary>
  LengthOverflow,

  /// <summary>Appending would exceed the buffer's fixed capacity.</summary>
  CapacityExceeded
}

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
