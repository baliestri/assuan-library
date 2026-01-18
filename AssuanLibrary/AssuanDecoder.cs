// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Utility;

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

    using var writer = new PooledByteWriter((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];

      if (c == '%' &&
          (i + 2) < value.Length) {
        var h1 = value[i + 1];
        var h2 = value[i + 2];

        var hi = HexToNibble(h1);
        var lo = HexToNibble(h2);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
        }
      }

      writer.Write((byte)c);
    }

    return writer.ToArray();
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

    using var writer = new PooledByteWriter((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var b = value[i];

      if (b == (byte)'%' &&
          (i + 2) < value.Length) {
        var h1 = (char)value[i + 1];
        var h2 = (char)value[i + 2];

        var hi = HexToNibble(h1);
        var lo = HexToNibble(h2);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
        }
      }

      writer.Write(b);
    }

    return writer.ToArray();
  }

  /// <summary>
  ///   Decodes the given ReadOnlyMemory&lt;byte&gt; according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The ReadOnlyMemory&lt;byte&gt; to decode.</param>
  /// <returns>The decoded byte array.</returns>
  public static byte[] ToBytes(ReadOnlyMemory<byte> value) {
    if (value.Length == 0) {
      return [];
    }

    using var writer = new PooledByteWriter((value.Length * 3) >> 2);
    var span = value.Span;

    for (var i = 0; i < span.Length; i++) {
      var b = span[i];

      if (b == (byte)'%' &&
          (i + 2) < span.Length) {
        var h1 = (char)span[i + 1];
        var h2 = (char)span[i + 2];

        var hi = HexToNibble(h1);
        var lo = HexToNibble(h2);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
        }
      }

      writer.Write(b);
    }

    return writer.ToArray();
  }

  /// <summary>
  ///   Decodes the given string according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The string to decode.</param>
  /// <returns>The decoded ReadOnlyMemory&lt;byte&gt;.</returns>
  public static ReadOnlyMemory<byte> ToReadOnlyMemory(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
      return ReadOnlyMemory<byte>.Empty;
    }

    using var writer = new PooledByteWriter((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];

      if (c == '%' &&
          (i + 2) < value.Length) {
        var h1 = value[i + 1];
        var h2 = value[i + 2];

        var hi = HexToNibble(h1);
        var lo = HexToNibble(h2);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
        }
      }

      writer.Write((byte)c);
    }

    return writer.ToReadOnlyMemory();
  }

  /// <summary>
  ///   Decodes the given byte array according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The byte array to decode.</param>
  /// <returns>The decoded ReadOnlyMemory&lt;byte&gt;.</returns>
  public static ReadOnlyMemory<byte> ToReadOnlyMemory(byte[] value) {
    if (value.Length == 0) {
      return ReadOnlyMemory<byte>.Empty;
    }

    using var writer = new PooledByteWriter((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var b = value[i];

      if (b == (byte)'%' &&
          (i + 2) < value.Length) {
        var h1 = (char)value[i + 1];
        var h2 = (char)value[i + 2];

        var hi = HexToNibble(h1);
        var lo = HexToNibble(h2);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
        }
      }

      writer.Write(b);
    }

    return writer.ToReadOnlyMemory();
  }

  /// <summary>
  ///   Decodes the given ReadOnlyMemory&lt;byte&gt; according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The ReadOnlyMemory&lt;byte&gt; to decode.</param>
  /// <returns>The decoded ReadOnlyMemory&lt;byte&gt;.</returns>
  public static ReadOnlyMemory<byte> ToReadOnlyMemory(ReadOnlyMemory<byte> value) {
    if (value.Length == 0) {
      return ReadOnlyMemory<byte>.Empty;
    }

    using var writer = new PooledByteWriter((value.Length * 3) >> 2);
    var span = value.Span;

    for (var i = 0; i < span.Length; i++) {
      var b = span[i];

      if (b == (byte)'%' &&
          (i + 2) < span.Length) {
        var h1 = (char)span[i + 1];
        var h2 = (char)span[i + 2];

        var hi = HexToNibble(h1);
        var lo = HexToNibble(h2);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
        }
      }

      writer.Write(b);
    }

    return writer.ToReadOnlyMemory();
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

    using var writer = new PooledStringWriter((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var c = value[i];

      if (c == '%') {
        if ((i + 2) < value.Length) {
          var h1 = value[i + 1];
          var h2 = value[i + 2];

          var b = (HexToNibble(h1) << 4) | HexToNibble(h2);

          if (b is not -1) {
            writer.Write((char)b);
            i += 2;
            continue;
          }
        }
      }

      writer.Write(c);
    }

    return writer.ToString();
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

    using var writer = new PooledStringWriter((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var b = value[i];

      if (b == (byte)'%') {
        if ((i + 2) < value.Length) {
          var h1 = (char)value[i + 1];
          var h2 = (char)value[i + 2];

          var decodedByte = (HexToNibble(h1) << 4) | HexToNibble(h2);

          if (decodedByte is not -1) {
            writer.Write((char)decodedByte);
            i += 2;
            continue;
          }
        }
      }

      writer.Write((char)b);
    }

    return writer.ToString();
  }

  /// <summary>
  ///   Decodes the given ReadOnlyMemory&lt;byte&gt; according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The ReadOnlyMemory&lt;byte&gt; to decode.</param>
  /// <returns>The decoded string.</returns>
  public static string ToString(ReadOnlyMemory<byte> value) {
    if (value.Length == 0) {
      return string.Empty;
    }

    using var writer = new PooledStringWriter((value.Length * 3) >> 2);
    var span = value.Span;

    for (var i = 0; i < span.Length; i++) {
      var b = span[i];

      if (b == (byte)'%') {
        if ((i + 2) < span.Length) {
          var h1 = (char)span[i + 1];
          var h2 = (char)span[i + 2];

          var decodedByte = (HexToNibble(h1) << 4) | HexToNibble(h2);

          if (decodedByte is not -1) {
            writer.Write((char)decodedByte);
            i += 2;
            continue;
          }
        }
      }

      writer.Write((char)b);
    }

    return writer.ToString();
  }
}
