// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Diagnostics;

/// <summary>Stable IDs for safe lifecycle observations.</summary>
public enum AssuanEventId {
  /// <summary>A server final response was written.</summary>
  FinalResponse = 1,

  /// <summary>A client observed a remote command ERR.</summary>
  RemoteFinal = 2,

  /// <summary>Channel read or framing failed.</summary>
  ReadFailed = 3,

  /// <summary>Channel write or flush failed.</summary>
  WriteFailed = 4
}
