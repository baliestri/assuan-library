// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>Category of an Assuan server line, without data bytes.</summary>
public enum ProtocolResponseKind {
  /// <summary>Successful final response.</summary>
  Ok,

  /// <summary>Final response containing a complete remote error code.</summary>
  Err,

  /// <summary>Percent-encoded data line.</summary>
  Data,

  /// <summary>Nonfinal informational status line.</summary>
  Status,

  /// <summary>Nonfinal request for client data.</summary>
  Inquire,

  /// <summary>Partial end marker, not a final response.</summary>
  End,

  /// <summary>Comment line.</summary>
  Comment,

  /// <summary>Empty framed line.</summary>
  Empty
}
