// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
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
