// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Metadata registration failed; the previous registry remains unchanged.</summary>
public sealed class RegistryException : Exception {
  internal RegistryException(RegistryErrorKind kind) : base($"command registration failed: {kind}") => Kind = kind;

  /// <summary>The failure category without handler metadata.</summary>
  public RegistryErrorKind Kind { get; }
}
