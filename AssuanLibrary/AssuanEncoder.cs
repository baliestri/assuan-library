// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary;

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

    table['='] = false;
    table['%'] = false;
    return table;
  }

  /// <summary>
  ///   Encodes the given string according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to encode.</param>
  /// <returns>The encoded string.</returns>
  public static string AsString(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      return string.Empty;
    }

    var stringBuilder = new StringBuilder();

    foreach (var c in value) {
      if (c < 128 &&
          _isSafeChar[c]) {
        stringBuilder.Append(c);
        continue;
      }

      stringBuilder.Append('%');
      stringBuilder.Append(_hexLookup[(c >> 4) & 0xF]);
      stringBuilder.Append(_hexLookup[c & 0xF]);
    }

    return stringBuilder.ToString();
  }

  /// <summary>
  ///   Encodes the given string into a byte array according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to encode.</param>
  /// <returns>The encoded byte array.</returns>
  public static byte[] AsBytes(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      return [];
    }

    var result = new List<byte>((value.Length * 3) / 2);

    foreach (var c in value) {
      if (c < 128 &&
          _isSafeChar[c]) {
        result.Add((byte)c);
        continue;
      }

      result.Add((byte)'%');
      result.Add((byte)_hexLookup[(c >> 4) & 0xF]);
      result.Add((byte)_hexLookup[c & 0xF]);
    }

    return result.ToArray();
  }
}
