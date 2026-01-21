// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary;

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
  extension(AssuanResponseType) {
    /// <summary>
    ///   Parses a byte array to determine the corresponding AssuanResponseType.
    /// </summary>
    /// <param name="buffer">The byte array representing the response type.</param>
    /// <returns>The corresponding AssuanResponseType.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="buffer" /> length exceeds 3 bytes.</exception>
    /// <exception cref="NotSupportedException">Thrown when the response type is not supported.</exception>
    public static AssuanResponseType Parse(byte[] buffer) {
      if (buffer.Length is < 1 or > 3) {
        throw new ArgumentOutOfRangeException(nameof(buffer), buffer.Length, "Response type buffer length must be at most 3 bytes.");
      }

      var prefix = buffer.AsSpan(0, Math.Min(3, buffer.Length));

      return prefix switch {
        var _ when prefix.SequenceEqual("OK"u8) => AssuanResponseType.Ok,
        var _ when prefix.SequenceEqual("ERR"u8) => AssuanResponseType.Error,
        var _ when prefix.SequenceEqual("S"u8) => AssuanResponseType.Status,
        var _ when prefix.SequenceEqual("#"u8) => AssuanResponseType.Comment,
        var _ when prefix.SequenceEqual("D"u8) => AssuanResponseType.Data,
        var _ => throw new NotSupportedException($"Unknown response type starting with: '{Encoding.UTF8.GetString(prefix)}'")
      };
    }
  }
}
