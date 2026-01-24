// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Extensions;

namespace AssuanLibrary;

/// <summary>
///   Represents a response from the Assuan protocol.
/// </summary>
public sealed class AssuanResponse : IEquatable<AssuanResponse> {
  private readonly byte[] _buffer;

  internal AssuanResponse(byte[] buffer) {
    _buffer = buffer;

    if (buffer.Length == 0) {
      Type = AssuanResponseType.Unknown;
      Buffer = [];
      return;
    }

    var type = AssuanResponseType.Parse(buffer.Take(Characters.SPACE));
    var responseBuffer = buffer.Skip(Characters.SPACE);

    Type = type;
    Buffer = responseBuffer;
  }

  /// <summary>
  ///   Gets the type of the response.
  /// </summary>
  public AssuanResponseType Type { get; }

  /// <summary>
  ///   Gets the original response buffer without the type prefix.
  /// </summary>
  public byte[] Buffer { get; }

  /// <summary>
  ///   Gets the decoded response buffer.
  /// </summary>
  public byte[] DecodedBuffer => AssuanDecoder.ToBytes(Buffer);

  /// <inheritdoc />
  public bool Equals(AssuanResponse? other) {
    if (other is null) {
      return false;
    }

    return ReferenceEquals(this, other) ||
           (Type == other.Type && Buffer.SequenceEqual(other.Buffer));
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is AssuanResponse response && Equals(response);

  /// <inheritdoc />
  public override int GetHashCode() {
    var typeHashCode = Type.GetHashCode();
    var bufferHashCode = Buffer.GetSequenceHashCode();

    return typeHashCode ^ bufferHashCode;
  }

  /// <summary>
  ///   Gets a copy of the original response buffer.
  /// </summary>
  /// <returns>A copy of the original response buffer.</returns>
  public byte[] GetOriginalBuffer()
    => _buffer.AsSpan().ToArray();

  /// <inheritdoc />
  public override string ToString()
    => ToString();

  /// <summary>
  ///   Gets the string representation of the response.
  /// </summary>
  /// <param name="dataToHex"><see langword="true" /> to convert data responses to hexadecimal string; otherwise, <see langword="false" />.</param>
  /// <returns>The string representation of the response.</returns>
  public string ToString(bool dataToHex = true) {
    return dataToHex && Type is AssuanResponseType.Data
      ? Convert.ToHexString(DecodedBuffer)
      : AssuanDecoder.ToString(Buffer);
  }
}
