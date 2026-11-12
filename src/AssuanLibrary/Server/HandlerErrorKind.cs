// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Whether a handler requested a validated public remote error or failed internally.</summary>
public enum HandlerErrorKind {
  /// <summary>A validated public error intended for the peer.</summary>
  Remote,

  /// <summary>An internal failure requiring a generic remote message.</summary>
  Internal
}
