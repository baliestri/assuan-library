// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Server;

/// <summary>A controlled handler failure. Diagnostics never interpolate remote text.</summary>
public sealed class HandlerException : Exception {
  private readonly static UTF8Encoding StrictUtf8 = new(false, true);

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
  [SuppressMessage("Design", "MA0054:Embed the caught exception as innerException",
    Justification = "An EncoderFallbackException message quotes the rejected character; " +
                    "diagnostics of this type must never carry the remote text.")]
  public static HandlerException Remote(uint code, string publicText) {
    ArgumentNullException.ThrowIfNull(publicText);

    try {
      var bytes = StrictUtf8.GetBytes(publicText);
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
