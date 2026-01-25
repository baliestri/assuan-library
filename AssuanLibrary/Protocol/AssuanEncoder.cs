// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Buffers;

namespace AssuanLibrary.Protocol;

/// <summary>
///   Provides methods to encode strings and byte arrays according to the Assuan protocol.
/// </summary>
public static class AssuanEncoder {
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
  /// <param name="encodeSpace">Whether to encode space characters as %20.</param>
  /// <returns>The encoded string.</returns>
  public static string AsString(string value, bool appendLineFeed = true, bool encodeSpace = false) {
    if (string.IsNullOrWhiteSpace(value)) {
      return string.Empty;
    }

    using var writer = new PooledStringWriter((value.Length * 3) / 2);

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];
      if (c < 128 &&
          _isSafeChar[c] &&
          (!encodeSpace || c != ' ')) {
        writer.Write(c);
        continue;
      }

      if (c == '%' &&
          (i + 2) < value.Length &&
          IsHexChar(value[i + 1]) &&
          IsHexChar(value[i + 2])) {
        writer.Write('%');
        writer.Write(value[i + 1]);
        writer.Write(value[i + 2]);
        i += 2;
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

    using var writer = new PooledByteWriter((value.Length * 3) / 2);

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];
      if (c < 128 &&
          _isSafeChar[c]) {
        writer.Write((byte)c);
        continue;
      }

      if (c == '%' &&
          (i + 2) < value.Length &&
          IsHexChar(value[i + 1]) &&
          IsHexChar(value[i + 2])) {
        writer.Write(Characters.PERCENT);
        writer.Write((byte)value[i + 1]);
        writer.Write((byte)value[i + 2]);
        i += 2;
        continue;
      }

      writer.Write(Characters.PERCENT);
      writer.Write((byte)_hexLookup[(c >> 4) & 0xF]);
      writer.Write((byte)_hexLookup[c & 0xF]);
    }

    if (appendLineFeed) {
      writer.Write(Characters.LINE_FEED);
    }

    return writer.ToArray();
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

    using var writer = new PooledByteWriter((value.Length * 3) / 2);

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];
      if (c < 128 &&
          _isSafeChar[c]) {
        writer.Write((byte)c);
        continue;
      }

      if (c == '%' &&
          (i + 2) < value.Length &&
          IsHexChar(value[i + 1]) &&
          IsHexChar(value[i + 2])) {
        writer.Write(Characters.PERCENT);
        writer.Write((byte)value[i + 1]);
        writer.Write((byte)value[i + 2]);
        i += 2;
        continue;
      }

      writer.Write(Characters.PERCENT);
      writer.Write((byte)_hexLookup[(c >> 4) & 0xF]);
      writer.Write((byte)_hexLookup[c & 0xF]);
    }

    if (appendLineFeed) {
      writer.Write(Characters.LINE_FEED);
    }

    return writer.ToReadOnlyMemory();
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

    using var writer = new PooledByteWriter((value.Length * 3) / 2);

    for (var i = 0; i < value.Length; i++) {
      var b = value[i];
      if (b < 128 &&
          _isSafeChar[b]) {
        writer.Write(b);
        continue;
      }

      if (b == '%' &&
          (i + 2) < value.Length &&
          IsHexChar((char)value[i + 1]) &&
          IsHexChar((char)value[i + 2])) {
        writer.Write(Characters.PERCENT);
        writer.Write(value[i + 1]);
        writer.Write(value[i + 2]);
        i += 2;
        continue;
      }

      writer.Write(Characters.PERCENT);
      writer.Write((byte)_hexLookup[(b >> 4) & 0xF]);
      writer.Write((byte)_hexLookup[b & 0xF]);
    }

    if (appendLineFeed) {
      writer.Write(Characters.LINE_FEED);
    }

    return writer.ToReadOnlyMemory();
  }

  private static bool IsHexChar(char c)
    => _hexLookup.Contains(char.ToUpperInvariant(c));
}
