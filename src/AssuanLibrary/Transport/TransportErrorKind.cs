// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport;

/// <summary>The category of a transport failure without bytes or external error text.</summary>
public enum TransportErrorKind {
  /// <summary>The total operation deadline expired.</summary>
  Timeout,

  /// <summary>The caller canceled the operation.</summary>
  Cancelled,

  /// <summary>The channel or listener was closed.</summary>
  Closed,

  /// <summary>The required local access policy could not be established or verified.</summary>
  AccessPolicy,

  /// <summary>The requested address is invalid for this transport.</summary>
  InvalidEndpoint,

  /// <summary>The requested timeout or other option is unsupported.</summary>
  InvalidOptions,

  /// <summary>The operating system or underlying stream reported an I/O failure.</summary>
  Io,

  /// <summary>Wire-line framing failed.</summary>
  Framing
}
