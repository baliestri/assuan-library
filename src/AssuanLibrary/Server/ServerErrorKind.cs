// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Non-sensitive category of a session failure.</summary>
public enum ServerErrorKind {
  /// <summary>A configured limit or deadline is invalid.</summary>
  InvalidOptions,

  /// <summary>Wire data or session sequencing is invalid.</summary>
  Protocol,

  /// <summary>Transport I/O or admission failed.</summary>
  Transport,

  /// <summary>An inquiry resource limit was exceeded.</summary>
  Limit,

  /// <summary>Operation was cancelled.</summary>
  Cancelled,

  /// <summary>A total deadline expired.</summary>
  Timeout,

  /// <summary>Handler or hook failed.</summary>
  Handler,

  /// <summary>Session cannot be used after closure.</summary>
  Closed
}
