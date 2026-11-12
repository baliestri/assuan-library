// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client;

/// <summary>Category of one borrowed response event.</summary>
public enum ClientEventKind {
  /// <summary>A percent-decoded D line.</summary>
  Data,

  /// <summary>An S line with keyword and literal arguments.</summary>
  Status,

  /// <summary>A comment line without its leading #.</summary>
  Comment,

  /// <summary>A partial END line, not the final response.</summary>
  End,

  /// <summary>An exclusive server inquiry requiring END or CAN.</summary>
  Inquire,

  /// <summary>A final OK or ERR response.</summary>
  Finished
}
