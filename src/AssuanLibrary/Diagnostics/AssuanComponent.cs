// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Diagnostics;

/// <summary>Which layer reports a payload-free operation result.</summary>
public enum AssuanComponent {
  /// <summary>A server-session operation.</summary>
  Server,

  /// <summary>A client transaction.</summary>
  Client,

  /// <summary>Underlying channel I/O.</summary>
  Transport
}
