// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Client;

/// <summary>Payload-free category of a client failure.</summary>
public enum ClientErrorKind {
  /// <summary>A configured duration or limit is invalid.</summary>
  InvalidOptions,

  /// <summary>A remote ERR rejected the greeting.</summary>
  GreetingRejected,

  /// <summary>Wire bytes failed protocol validation.</summary>
  Protocol,

  /// <summary>The connection cannot perform this transition.</summary>
  State,

  /// <summary>An interactive inquiry could not be handled.</summary>
  [SuppressMessage("ReSharper", "UnusedMember.Global",
    Justification = "Published enum value; removing it would renumber the public members that follow.")]
  UnsupportedInquiry,

  /// <summary>Final response was not observed before completion was requested.</summary>
  Incomplete,

  /// <summary>A configured byte or event budget was exceeded.</summary>
  Limit,

  /// <summary>A completed command returned a remote ERR and may be reused.</summary>
  Remote,

  /// <summary>A total operation deadline expired.</summary>
  Timeout,

  /// <summary>The caller cancelled the operation.</summary>
  Cancelled,

  /// <summary>Underlying transport I/O failed.</summary>
  Io
}
