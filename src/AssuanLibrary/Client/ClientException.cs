// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Client;

/// <summary>A client failure whose diagnostics omit wire content and external error text.</summary>
public sealed class ClientException : Exception {
  internal ClientException(ClientErrorKind kind, uint? remoteCode = null, Exception? technicalCause = null)
    : base($"client operation failed: {kind}{(remoteCode.HasValue ? $" ({remoteCode.Value})" : "")}") {
    Kind = kind;
    RemoteCode = remoteCode;
    TechnicalCause = technicalCause;
  }

  /// <summary>Failure category.</summary>
  public ClientErrorKind Kind { get; }

  /// <summary>Full unsigned code of a remote ERR when present.</summary>
  public uint? RemoteCode { get; }

  /// <summary>Optional external cause; redact it separately before logging.</summary>
  [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global",
    Justification = "Public diagnostic surface read by consumers, never by the library itself.")]
  public Exception? TechnicalCause { get; }
}
