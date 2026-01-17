// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary;

public static class AssuanDecoder {
  private static int HexToNibble(char c) {
    return c switch {
      >= '0' and <= '9' => c - '0',
      >= 'A' and <= 'F' => (c - 'A') + 10,
      >= 'a' and <= 'f' => (c - 'a') + 10,
      var _ => -1
    };
  }

  /// <summary>
  ///   Decodes the given string according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to decode.</param>
  /// <returns>The decoded byte array.</returns>
  public static byte[] ToBytes(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      return [];
    }

    var bytes = new List<byte>((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];

      if (c == '%') {
        if ((i + 2) < value.Length) {
          var h1 = value[i + 1];
          var h2 = value[i + 2];

          var b = (HexToNibble(h1) << 4) | HexToNibble(h2);

          if (b is not -1) {
            bytes.Add((byte)b);
            i += 2;
            continue;
          }
        }
      }

      bytes.Add((byte)c);
    }

    return bytes.ToArray();
  }

  /// <summary>
  ///   Decodes the given byte array according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The byte array to decode.</param>
  /// <returns>The decoded byte array.</returns>
  public static byte[] ToBytes(byte[] value) {
    if (value.Length == 0) {
      return [];
    }

    var bytes = new List<byte>((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var b = value[i];

      if (b == (byte)'%') {
        if ((i + 2) < value.Length) {
          var h1 = (char)value[i + 1];
          var h2 = (char)value[i + 2];

          var decodedByte = (HexToNibble(h1) << 4) | HexToNibble(h2);

          if (decodedByte is not -1) {
            bytes.Add((byte)decodedByte);
            i += 2;
            continue;
          }
        }
      }

      bytes.Add(b);
    }

    return bytes.ToArray();
  }

  /// <summary>
  ///   Decodes the given string according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to decode.</param>
  /// <returns>The decoded string.</returns>
  public static string ToString(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      return string.Empty;
    }

    var stringBuilder = new StringBuilder();

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];

      if (c == '%') {
        if ((i + 2) < value.Length) {
          var h1 = value[i + 1];
          var h2 = value[i + 2];

          var b = (HexToNibble(h1) << 4) | HexToNibble(h2);

          if (b is not -1) {
            stringBuilder.Append((char)b);
            i += 2;
            continue;
          }
        }
      }

      stringBuilder.Append(c);
    }

    return stringBuilder.ToString();
  }

  /// <summary>
  ///   Decodes the given byte array according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The byte array to decode.</param>
  /// <returns>The decoded string.</returns>
  public static string ToString(byte[] value) {
    if (value.Length == 0) {
      return string.Empty;
    }

    var stringBuilder = new StringBuilder();

    for (var i = 0; i < value.Length; i++) {
      var b = value[i];

      if (b == (byte)'%') {
        if ((i + 2) < value.Length) {
          var h1 = (char)value[i + 1];
          var h2 = (char)value[i + 2];

          var decodedByte = (HexToNibble(h1) << 4) | HexToNibble(h2);

          if (decodedByte is not -1) {
            stringBuilder.Append((char)decodedByte);
            i += 2;
            continue;
          }
        }
      }

      stringBuilder.Append((char)b);
    }

    return stringBuilder.ToString();
  }
}
