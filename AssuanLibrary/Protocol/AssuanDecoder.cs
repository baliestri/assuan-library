// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol.Buffers;

namespace AssuanLibrary.Protocol;

public static class AssuanDecoder {
  private static int HexToNibble(byte b)
    => HexToNibble((char)b);

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
      var b = value[i];

      if (b == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(value[i + 1]);
        var lo = HexToNibble(value[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
          continue;
        }
      }

      writer.Write((byte)b);
    }

    return writer.ToArray();
  }

  /// <summary>
  ///   Decodes the given byte array according to the Assuan protocol.
  /// </summary>
  /// <param name="value">The ReadOnlySpan&lt;byte&gt; to decode.</param>
  /// <returns>The decoded byte array.</returns>
  public static byte[] ToBytes(ReadOnlySpan<byte> value) {
    if (value.IsEmpty) {
      return [];
    }

    using var writer = new PooledByteWriter((value.Length * 3) >> 2);

    for (var i = 0; i < value.Length; i++) {
      var b = value[i];

      if (b == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(value[i + 1]);
        var lo = HexToNibble(value[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
          continue;
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

    for (var i = 0; i < value.Length; i++) {
      var b = span[i];

      if (b == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(span[i + 1]);
        var lo = HexToNibble(span[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
          continue;
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

      if (c == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(value[i + 1]);
        var lo = HexToNibble(value[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
          continue;
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

      if (b == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(value[i + 1]);
        var lo = HexToNibble(value[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
          continue;
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

    for (var i = 0; i < value.Length; i++) {
      var b = span[i];

      if (b == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(span[i + 1]);
        var lo = HexToNibble(span[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((byte)((hi << 4) | lo));
          i += 2;
          continue;
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

      if (c == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(value[i + 1]);
        var lo = HexToNibble(value[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((char)((hi << 4) | lo));
          i += 2;
          continue;
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

      if (b == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(value[i + 1]);
        var lo = HexToNibble(value[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((char)((hi << 4) | lo));
          i += 2;
          continue;
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

    for (var i = 0; i < value.Length; i++) {
      var b = span[i];

      if (b == Characters.PERCENT &&
          (i + 2) < value.Length) {
        var hi = HexToNibble(span[i + 1]);
        var lo = HexToNibble(span[i + 2]);

        if (hi >= 0 &&
            lo >= 0) {
          writer.Write((char)((hi << 4) | lo));
          i += 2;
          continue;
        }
      }

      writer.Write((char)b);
    }

    return writer.ToString();
  }

  /// <summary>
  ///   Get the inquire parameters from the given buffer.
  /// </summary>
  /// <param name="buffer">The buffer containing the inquire parameters.</param>
  /// <returns>The array of inquire parameters.</returns>
  public static string[] GetInquireParameters(ReadOnlySpan<byte> buffer) {
    if (buffer.IsEmpty) {
      return [];
    }

    var parameters = new List<string>();

    var i = 0;
    while (i < buffer.Length) {
      while (i < buffer.Length &&
             buffer[i] is Characters.SPACE or Characters.TABULATION) {
        i++;
      }

      if (i >= buffer.Length) {
        break;
      }

      var start = i;
      while (i < buffer.Length) {
        if (buffer[i] is Characters.SPACE or Characters.TABULATION) {
          break;
        }

        i += buffer[i] == Characters.PERCENT && (i + 2) < buffer.Length
          ? 3
          : 1;
      }

      var parameterSpan = buffer[start..i];
      var parameter = ToString(parameterSpan.ToArray());
      parameters.Add(parameter);
    }

    return parameters
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .ToArray();
  }
}
