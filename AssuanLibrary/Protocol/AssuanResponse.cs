// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.Extensions;
using AssuanLibrary.Protocol.Buffers;

namespace AssuanLibrary.Protocol;

/// <summary>
///   Represents a response from the Assuan protocol.
/// </summary>
public sealed partial class AssuanResponse : IEquatable<AssuanResponse>, IFormattable {
  private readonly byte[] _buffer;

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanResponse" /> class from the given buffer.
  /// </summary>
  /// <param name="buffer">The response buffer.</param>
  public AssuanResponse(byte[] buffer) {
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

  private AssuanResponse(AssuanResponseType type, byte[] buffer) {
    Type = type;
    Buffer = buffer;

    using var writer = new PooledByteWriter(buffer.Length + 8);

    writer.Write(type.ToBytesRepresentation());

    if (Buffer.Length != 0) {
      writer.Write(Characters.SPACE);
      writer.Write(Buffer);
    }

    _buffer = writer.ToArray();
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

  /// <summary>
  ///   Gets the length of the original response buffer.
  /// </summary>
  public int Length => _buffer.Length;

  /// <inheritdoc />
  public bool Equals(AssuanResponse? other) {
    if (other is null) {
      return false;
    }

    return ReferenceEquals(this, other) ||
           (Type == other.Type && Buffer.SequenceEqual(other.Buffer));
  }

  /// <inheritdoc />
  /// <remarks>
  ///   The supported format specifiers are:
  ///   <list type="bullet">
  ///     <item>
  ///       <description>T: Includes the response type in the output.</description>
  ///     </item>
  ///     <item>
  ///       <description>H: Includes the raw hexadecimal representation of the response buffer.</description>
  ///     </item>
  ///     <item>
  ///       <description>D: Includes the decoded hexadecimal representation of the response buffer.</description>
  ///     </item>
  ///     <item>
  ///       <description>G: Includes the decoded string representation of the response buffer.</description>
  ///     </item>
  ///   </list>
  ///   If no format specifier is provided, the default is 'D' for data responses and 'G' for other types.
  /// </remarks>
  public string ToString(string? format, IFormatProvider? formatProvider) {
    if (string.IsNullOrWhiteSpace(format) ||
        format!.Length > 2) {
      format = Type is AssuanResponseType.Data ? "D" : "G";
    }

    var shouldSpecifyContent = format.IndexOf('H') != -1 || format.IndexOf('G') != -1 || format.IndexOf('D') != -1;
    var shouldSpecifyType = format.IndexOf('T') != -1;
    var contentSpecifierIndex = format.IndexOfAny(['H', 'G', 'D']);
    var contentSpecifier = contentSpecifierIndex != -1
      ? format[contentSpecifierIndex]
      : char.MinValue;

    var content = contentSpecifier switch {
      'D' when shouldSpecifyContent && Type is AssuanResponseType.Data => Convert.ToHexString(DecodedBuffer),
      'H' when shouldSpecifyContent => Convert.ToHexString(DecodedBuffer),
      'D' or 'G' when shouldSpecifyContent => AssuanDecoder.ToString(Buffer),
      var _ when shouldSpecifyContent => throw new FormatException($"The format string '{format}' is not supported."),
      var _ => string.Empty
    };

    return shouldSpecifyType switch {
      true when shouldSpecifyContent => $"{Type.ToStringRepresentation()} {content}",
      true => Type.ToStringRepresentation(),
      var _ => content
    };
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
    => ToString(null, CultureInfo.CurrentCulture);
}
