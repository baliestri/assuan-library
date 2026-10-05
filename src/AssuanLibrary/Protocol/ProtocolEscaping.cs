// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>Binary percent escape operations for Assuan data lines.</summary>
public static class ProtocolEscaping {
  /// <summary>Validates all escapes before decoding in place; only the returned prefix is decoded.</summary>
  /// <remarks>Bytes beyond the returned prefix may still contain old data. Wipe the owner's entire buffer if sensitive.</remarks>
  public static int DecodeDataInPlace(Span<byte> bytes) {
    for (var read = 0; read < bytes.Length; ++read) {
      if (bytes[read] != (byte)'%') {
        continue;
      }

      if ((bytes.Length - read) < 3 ||
          Hex(bytes[read + 1]) < 0 ||
          Hex(bytes[read + 2]) < 0) {
        throw new ProtocolException(ProtocolErrorKind.InvalidEscape);
      }

      read += 2;
    }

    var written = 0;
    for (var read = 0; read < bytes.Length; ++read) {
      if (bytes[read] == (byte)'%') {
        bytes[written++] = (byte)((Hex(bytes[read + 1]) * 16) + Hex(bytes[read + 2]));
        read += 2;
      }
      else {
        bytes[written++] = bytes[read];
      }
    }

    return written;
  }

  internal static void ValidateEncodedData(ReadOnlySpan<byte> bytes) {
    for (var index = 0; index < bytes.Length; ++index) {
      if (bytes[index] != (byte)'%') {
        continue;
      }

      if ((bytes.Length - index) < 3 ||
          Hex(bytes[index + 1]) < 0 ||
          Hex(bytes[index + 2]) < 0) {
        throw new ProtocolException(ProtocolErrorKind.InvalidEscape);
      }

      index += 2;
    }
  }

  private static int Hex(byte digit) => digit switch {
    >= (byte)'0' and <= (byte)'9' => digit - (byte)'0',
    >= (byte)'a' and <= (byte)'f' => (digit - (byte)'a') + 10,
    >= (byte)'A' and <= (byte)'F' => (digit - (byte)'A') + 10,
    var _ => -1
  };
}
