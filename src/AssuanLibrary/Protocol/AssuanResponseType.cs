// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary.Protocol;

/// <summary>
///   Represents the type of response received from the Assuan protocol.
/// </summary>
public enum AssuanResponseType {
  /// <summary>
  ///   The response indicates a successful operation.
  /// </summary>
  Ok,

  /// <summary>
  ///   The response indicates an error occurred.
  /// </summary>
  Error,

  /// <summary>
  ///   The response contains a status message.
  /// </summary>
  Status,

  /// <summary>
  ///   The response contains a comment message.
  /// </summary>
  Comment,

  /// <summary>
  ///   The response contains data.
  /// </summary>
  Data,

  /// <summary>
  ///   The response is an inquire request.
  /// </summary>
  Inquire,

  /// <summary>
  ///   The response indicates the end of a data stream.
  /// </summary>
  End,

  /// <summary>
  ///   The response indicates that an operation can be performed.
  /// </summary>
  Cancel,

  /// <summary>
  ///   The response type is unknown.
  /// </summary>
  /// <remarks>
  ///   Should be used only in exceptional cases where the response type cannot be determined.
  /// </remarks>
  Unknown
}

/// <summary>
///   Extension methods for the <see cref="AssuanResponseType" />.
/// </summary>
public static class AssuanResponseTypeExtensions {
  extension(AssuanResponseType responseType) {
    /// <summary>
    ///   Parses a byte array to determine the corresponding AssuanResponseType.
    /// </summary>
    /// <param name="buffer">The byte array representing the response type.</param>
    /// <returns>The corresponding AssuanResponseType.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="buffer" /> length exceeds 3 bytes.</exception>
    /// <exception cref="NotSupportedException">Thrown when the response type is not supported.</exception>
    public static AssuanResponseType Parse(byte[] buffer) {
      if (buffer.Length is < 1 or > 7) {
        throw new ArgumentOutOfRangeException(nameof(buffer), buffer.Length, "Response type buffer length must be at most 7 bytes.");
      }

      var prefix = buffer.AsSpan(0, Math.Min(7, buffer.Length));

      var lineFeedIndex = prefix.IndexOf(Characters.LINE_FEED);
      if (lineFeedIndex is not -1) {
        prefix = prefix[..lineFeedIndex];
      }

      return prefix switch {
        var _ when prefix.SequenceEqual("OK"u8) => AssuanResponseType.Ok,
        var _ when prefix.SequenceEqual("ERR"u8) => AssuanResponseType.Error,
        var _ when prefix.SequenceEqual("S"u8) => AssuanResponseType.Status,
        var _ when prefix.SequenceEqual("#"u8) => AssuanResponseType.Comment,
        var _ when prefix.SequenceEqual("D"u8) => AssuanResponseType.Data,
        var _ when prefix.SequenceEqual("INQUIRE"u8) => AssuanResponseType.Inquire,
        var _ when prefix.SequenceEqual("END"u8) => AssuanResponseType.End,
        var _ when prefix.SequenceEqual("CANCEL"u8) => AssuanResponseType.Cancel,
        var _ => throw new NotSupportedException($"Unknown response type starting with: '{Encoding.UTF8.GetString(prefix)}'")
      };
    }

    internal string ToStringRepresentation() {
      return responseType switch {
        AssuanResponseType.Ok => "OK",
        AssuanResponseType.Error => "ERR",
        AssuanResponseType.Status => "S",
        AssuanResponseType.Comment => "#",
        AssuanResponseType.Data => "D",
        AssuanResponseType.Inquire => "INQUIRE",
        AssuanResponseType.End => "END",
        AssuanResponseType.Cancel => "CANCEL",
        var _ => throw new NotSupportedException($"The response type '{responseType}' is not supported.")
      };
    }

    internal byte[] ToBytesRepresentation() {
      return responseType switch {
        AssuanResponseType.Ok => "OK"u8.ToArray(),
        AssuanResponseType.Error => "ERR"u8.ToArray(),
        AssuanResponseType.Status => "S"u8.ToArray(),
        AssuanResponseType.Comment => "#"u8.ToArray(),
        AssuanResponseType.Data => "D"u8.ToArray(),
        AssuanResponseType.Inquire => "INQUIRE"u8.ToArray(),
        AssuanResponseType.End => "END"u8.ToArray(),
        AssuanResponseType.Cancel => "CANCEL"u8.ToArray(),
        var _ => throw new NotSupportedException($"The response type '{responseType}' is not supported.")
      };
    }
  }
}
