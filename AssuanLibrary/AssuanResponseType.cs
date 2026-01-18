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
  private static readonly Dictionary<byte[], AssuanResponseType> _responseTypes = new() {
    { "OK"u8.ToArray(), AssuanResponseType.Ok },
    { "ERR"u8.ToArray(), AssuanResponseType.Error },
    { "S"u8.ToArray(), AssuanResponseType.Status },
    { "#"u8.ToArray(), AssuanResponseType.Comment },
    { "D"u8.ToArray(), AssuanResponseType.Data }
  };

  extension(AssuanResponseType) {
    /// <summary>
    ///   Parses a byte array to determine the corresponding AssuanResponseType.
    /// </summary>
    /// <param name="buffer">The byte array representing the response type.</param>
    /// <returns>The corresponding AssuanResponseType.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="buffer" /> length exceeds 3 bytes.</exception>
    /// <exception cref="NotSupportedException">Thrown when the response type is not supported.</exception>
    public static AssuanResponseType Parse(byte[] buffer) {
      if (buffer.Length > 3) {
        throw new ArgumentOutOfRangeException(nameof(buffer), "Response type buffer length must be at most 3 bytes.");
      }

      return _responseTypes.TryGetValue(buffer, out var responseType)
        ? responseType
        : throw new NotSupportedException($"Response type '{Encoding.UTF8.GetString(buffer)}' is not supported.");
    }
  }
}
