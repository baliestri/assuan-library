// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

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

/// <summary>A client failure whose diagnostics omit wire content and external error text.</summary>
public sealed class ClientException : Exception {
  internal ClientException(ClientErrorKind kind, uint? remoteCode = null, Exception? technicalCause = null)
    : base($"client operation failed: {kind}" + (remoteCode.HasValue ? $" ({remoteCode.Value})" : "")) {
    Kind = kind;
    RemoteCode = remoteCode;
    TechnicalCause = technicalCause;
  }

  /// <summary>Failure category.</summary>
  public ClientErrorKind Kind { get; }

  /// <summary>Full unsigned code of a remote ERR when present.</summary>
  public uint? RemoteCode { get; }

  /// <summary>Optional external cause; redact it separately before logging.</summary>
  public Exception? TechnicalCause { get; }
}
