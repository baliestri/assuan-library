// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
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

    var bufferCopy = ArrayPool<byte>.Shared.Rent(buffer.Length);

    try {
      buffer.CopyTo(bufferCopy, 0);
      var type = AssuanResponseType.Parse(bufferCopy.Take(Characters.SPACE));
      var responseBuffer = bufferCopy.Skip(Characters.SPACE).Take(Characters.LF);

      Type = type;
      Buffer = responseBuffer;
    }
    finally {
      ArrayPool<byte>.Shared.Return(bufferCopy);
    }
  }

  /// <summary>
  ///   The type of the response.
  /// </summary>
  public AssuanResponseType Type { get; }

  /// <summary>
  ///   The original response buffer.
  /// </summary>
  /// <remarks>
  ///   This buffer does not contain the response type prefix.
  /// </remarks>
  public byte[] Buffer { get; }

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
  public byte[] GetOriginalBuffer() {
    var copy = ArrayPool<byte>.Shared.Rent(_buffer.Length);
    _buffer.CopyTo(copy, 0);
    return copy;
  }

  /// <inheritdoc />
  public override string ToString()
    => AssuanDecoder.ToString(Buffer);
}
