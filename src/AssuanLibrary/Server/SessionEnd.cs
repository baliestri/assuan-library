// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Payload-free reason for session closure.</summary>
public enum SessionEnd {
  /// <summary>The peer completed its session or disconnected between commands.</summary>
  Clean,

  /// <summary>Malformed input or incomplete protocol work.</summary>
  Protocol,

  /// <summary>Transport read or write failed.</summary>
  Transport,

  /// <summary>Operation was cancelled.</summary>
  Cancelled,

  /// <summary>A total operation deadline expired.</summary>
  Timeout,

  /// <summary>Authentication or application handler failed.</summary>
  Handler
}
