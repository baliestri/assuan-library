// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client;

/// <summary>Payload-free category of a client failure.</summary>
public enum ClientErrorKind {
  InvalidOptions, GreetingRejected, Protocol, State, UnsupportedInquiry, Incomplete,
  Limit, Remote, Timeout, Cancelled, Io
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
