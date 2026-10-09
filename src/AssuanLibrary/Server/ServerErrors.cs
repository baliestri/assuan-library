// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Server;

/// <summary>Non-sensitive category of a session failure.</summary>
public enum ServerErrorKind {
  /// <summary>A configured limit or deadline is invalid.</summary>
  InvalidOptions,

  /// <summary>Wire data or session sequencing is invalid.</summary>
  Protocol,

  /// <summary>Transport I/O or admission failed.</summary>
  Transport,

  /// <summary>An inquiry resource limit was exceeded.</summary>
  Limit,

  /// <summary>Operation was cancelled.</summary>
  Cancelled,

  /// <summary>A total deadline expired.</summary>
  Timeout,

  /// <summary>Handler or hook failed.</summary>
  Handler,

  /// <summary>Session cannot be used after closure.</summary>
  Closed
}

/// <summary>A server-session error without command or payload bytes in its diagnostics.</summary>
public sealed class ServerException : Exception {
  internal ServerException(ServerErrorKind kind, Exception? technicalCause = null)
    : base($"server session failed: {kind}") {
    Kind = kind;
    TechnicalCause = technicalCause;
  }

  /// <summary>The failure category.</summary>
  public ServerErrorKind Kind { get; }

  /// <summary>Optional external cause; redact its text before displaying or logging it.</summary>
  public Exception? TechnicalCause { get; }
}

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

/// <summary>Metadata registration failed; the previous registry remains unchanged.</summary>
public sealed class RegistryException : Exception {
  internal RegistryException(RegistryErrorKind kind) : base($"command registration failed: {kind}") => Kind = kind;

  /// <summary>The failure category without handler metadata.</summary>
  public RegistryErrorKind Kind { get; }
}

/// <summary>Whether a handler requested a validated public remote error or failed internally.</summary>
public enum HandlerErrorKind {
  /// <summary>A validated public error intended for the peer.</summary>
  Remote,

  /// <summary>An internal failure requiring a generic remote message.</summary>
  Internal
}

/// <summary>A controlled handler failure. Diagnostics never interpolate remote text.</summary>
public sealed class HandlerException : Exception {
  private static readonly UTF8Encoding _strictUtf8 = new(false, true);

  private HandlerException(HandlerErrorKind kind, uint code = 0, string? text = null)
    : base($"handler failed: {kind}") {
    Kind = kind;
    Code = code;
    PublicText = text;
  }

  /// <summary>Whether the error may be sent to the peer.</summary>
  public HandlerErrorKind Kind { get; }

  /// <summary>Validated complete remote code, when <see cref="Kind" /> is Remote.</summary>
  public uint Code { get; }

  /// <summary>Explicitly requested public text; never included in exception diagnostics.</summary>
  public string? PublicText { get; }

  /// <summary>Validates a complete ERR line before allowing text to leave the process.</summary>
  public static HandlerException Remote(uint code, string publicText) {
    ArgumentNullException.ThrowIfNull(publicText);

    try {
      var bytes = _strictUtf8.GetBytes(publicText);
      Span<byte> output = stackalloc byte[1000];
      _ = ProtocolEncoder.EncodeResponse(ProtocolResponse.Err(code, bytes), output);
    }
    catch (Exception error) when (error is ProtocolException or EncoderFallbackException) {
      throw new ArgumentException("Remote error text cannot be encoded as one protocol line.", nameof(publicText));
    }

    return new HandlerException(HandlerErrorKind.Remote, code, publicText);
  }

  /// <summary>Requests the generic, redacted internal error code and text.</summary>
  public static HandlerException Internal()
    => new(HandlerErrorKind.Internal);
}
