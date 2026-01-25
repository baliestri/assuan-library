// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Buffers;

namespace AssuanLibrary.Protocol;

/// <summary>
///   Provides methods to encode strings and byte arrays according to the Assuan protocol.
/// </summary>
public static class AssuanEncoder {
  private const byte ESCAPE_SPACE_CHAR = (byte)'¨';
  private static readonly char[] _hexLookup = "0123456789ABCDEF".ToCharArray();
  private static readonly bool[] _isSafeChar = CreateSafeCharTable();

  private static bool[] CreateSafeCharTable() {
    var table = new bool[128];
    for (var i = 32; i <= 126; i++) {
      table[i] = true;
    }

    table['%'] = false;
    return table;
  }

  /// <summary>
  ///   Encodes the given string according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to encode.</param>
  /// <param name="appendLineFeed">Whether to append a line feed character at the end.</param>
  /// <returns>The encoded string.</returns>
  public static string AsString(string value, bool appendLineFeed = true) {
    if (string.IsNullOrWhiteSpace(value)) {
      return string.Empty;
    }

    using var writer = new PooledStringWriter((value.Length * 3) / 2);

    var escapeSpace = false;
    foreach (var c in value) {
      if (c == ESCAPE_SPACE_CHAR) {
        escapeSpace = !escapeSpace;
        continue;
      }

      if (c < 128 &&
          _isSafeChar[c] &&
          (!escapeSpace || c != ' ')) {
        writer.Write(c);
        continue;
      }

      writer.Write('%');
      writer.Write(_hexLookup[(c >> 4) & 0xF]);
      writer.Write(_hexLookup[c & 0xF]);
    }

    if (appendLineFeed) {
      writer.Write('\n');
    }

    return writer.ToString();
  }

  /// <summary>
  ///   Encodes the given string into a byte array according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to encode.</param>
  /// <param name="appendLineFeed">Whether to append a line feed character at the end.</param>
  /// <returns>The encoded byte array.</returns>
  public static byte[] AsBytes(string value, bool appendLineFeed = true) {
    if (string.IsNullOrWhiteSpace(value)) {
      return [];
    }

    using var buffer = new PooledByteWriter((value.Length * 3) / 2);

    var escapeSpace = false;
    foreach (var c in value) {
      if (c == ESCAPE_SPACE_CHAR) {
        escapeSpace = !escapeSpace;
        continue;
      }

      if (c < 128 &&
          _isSafeChar[c] &&
          (!escapeSpace || c != ' ')) {
        buffer.Write((byte)c);
        continue;
      }

      buffer.Write(Characters.PERCENT);
      buffer.Write((byte)_hexLookup[(c >> 4) & 0xF]);
      buffer.Write((byte)_hexLookup[c & 0xF]);
    }

    if (appendLineFeed) {
      buffer.Write(Characters.LINE_FEED);
    }

    return buffer.ToArray();
  }

  /// <summary>
  ///   Encodes the given string into a ReadOnlyMemory&lt;byte&gt; according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to encode.</param>
  /// <param name="appendLineFeed">Whether to append a line feed character at the end.</param>
  /// <returns>The encoded ReadOnlyMemory&lt;byte&gt;.</returns>
  public static ReadOnlyMemory<byte> AsReadOnlyMemory(string value, bool appendLineFeed = true) {
    if (string.IsNullOrWhiteSpace(value)) {
      return ReadOnlyMemory<byte>.Empty;
    }

    using var buffer = new PooledByteWriter((value.Length * 3) / 2);

    var escapeSpace = false;
    foreach (var c in value) {
      if (c == ESCAPE_SPACE_CHAR) {
        escapeSpace = !escapeSpace;
        continue;
      }

      if (c < 128 &&
          _isSafeChar[c] &&
          (!escapeSpace || c != ' ')) {
        buffer.Write((byte)c);
        continue;
      }

      buffer.Write(Characters.PERCENT);
      buffer.Write((byte)_hexLookup[(c >> 4) & 0xF]);
      buffer.Write((byte)_hexLookup[c & 0xF]);
    }

    if (appendLineFeed) {
      buffer.Write(Characters.LINE_FEED);
    }

    return buffer.ToReadOnlyMemory();
  }

  /// <summary>
  ///   Encodes the given byte array according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The byte array to encode.</param>
  /// <param name="appendLineFeed">Whether to append a line feed character at the end.</param>
  /// <returns>The encoded ReadOnlyMemory&lt;byte&gt;.</returns>
  public static ReadOnlyMemory<byte> AsReadOnlyMemory(byte[] value, bool appendLineFeed = true) {
    if (value.Length == 0) {
      return ReadOnlyMemory<byte>.Empty;
    }

    using var buffer = new PooledByteWriter((value.Length * 3) / 2);

    var escapeSpace = false;
    foreach (var b in value) {
      var c = (char)b;

      if (c == ESCAPE_SPACE_CHAR) {
        escapeSpace = !escapeSpace;
        continue;
      }

      if (c < 128 &&
          _isSafeChar[c] &&
          (!escapeSpace || c != ' ')) {
        buffer.Write(b);
        continue;
      }

      buffer.Write(Characters.PERCENT);
      buffer.Write((byte)_hexLookup[(b >> 4) & 0xF]);
      buffer.Write((byte)_hexLookup[b & 0xF]);
    }

    if (appendLineFeed) {
      buffer.Write(Characters.LINE_FEED);
    }

    return buffer.ToReadOnlyMemory();
  }
}
