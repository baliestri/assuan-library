// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Category of an agent-discovery failure without paths, output, or nonce bytes.</summary>
public enum DiscoveryErrorKind {
  /// <summary>The selected executable failed or exited unsuccessfully.</summary>
  ProcessFailed,

  /// <summary>The selected process or native socket file returned invalid data.</summary>
  InvalidOutput,

  /// <summary>Combined stdout and stderr exceeded the fixed byte budget.</summary>
  OutputLimit,

  /// <summary>The socket file uses a format that this library does not support.</summary>
  UnsupportedFormat,

  /// <summary>An external process, file or socket operation failed.</summary>
  Io,

  /// <summary>The operation exceeded its total deadline.</summary>
  Timeout,

  /// <summary>The caller canceled the operation.</summary>
  Cancelled
}
