// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Diagnostics;

/// <summary>Safe category of one completed operation.</summary>
public enum AssuanOutcome {
  /// <summary>Operation completed successfully.</summary>
  Success,

  /// <summary>A final remote error was observed or sent.</summary>
  RemoteError,

  /// <summary>The deadline expired.</summary>
  Timeout,

  /// <summary>The caller cancelled the operation.</summary>
  Cancelled,

  /// <summary>An unrelated transport or framing failure occurred.</summary>
  Failure
}
