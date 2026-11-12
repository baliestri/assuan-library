// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Category of failed command-handler registration.</summary>
public enum RegistryErrorKind {
  /// <summary>The command token is invalid.</summary>
  InvalidName,

  /// <summary>The public HELP description is invalid.</summary>
  InvalidDescription,

  /// <summary>The name belongs to a built-in command.</summary>
  Reserved,

  /// <summary>Another handler already owns this name.</summary>
  Duplicate,

  /// <summary>The registry has been frozen for serving.</summary>
  Frozen
}
