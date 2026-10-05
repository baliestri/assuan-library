// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>Writes bounded Assuan lines to caller-owned storage.</summary>
public static class ProtocolEncoder {
  internal const int MAX_LINE_BYTES = 1000;

  /// <summary>Encodes a validated command with a trailing LF; arguments are already in wire form.</summary>
  /// <remarks>A validation failure leaves every output byte unchanged. The caller owns output cleanup.</remarks>
  public static int EncodeCommand(ProtocolCommand command, Span<byte> output) {
    ValidateOutput(output);
    ProtocolFields.ValidateToken(command.NameBytes);
    ProtocolFields.ValidateLine(command.Arguments);

    return command.Arguments.IsEmpty
      ? WriteParts(output, command.NameBytes)
      : WriteParts(output, command.NameBytes, " "u8, command.Arguments);
  }

  /// <summary>Encodes one server response with a trailing LF, validating all fields before writing.</summary>
  /// <remarks>A Data response contains already percent-encoded bytes; use <see cref="EncodeDataChunk" /> for raw data.</remarks>
  public static int EncodeResponse(ProtocolResponse response, Span<byte> output) {
    ValidateOutput(output);
    switch (response.Kind) {
      case ProtocolResponseKind.Ok:
        return response.PayloadBytes.IsEmpty
          ? WriteParts(output, "OK"u8)
          : WriteParts(output, "OK "u8, response.PayloadBytes);
      case ProtocolResponseKind.Err: {
        Span<byte> digits = stackalloc byte[10];
        var first = digits.Length;
        var code = response.ErrorCode;
        do {
          digits[--first] = (byte)('0' + (code % 10));
          code /= 10;
        } while (code != 0);

        return response.PayloadBytes.IsEmpty
          ? WriteParts(output, "ERR "u8, digits[first..])
          : WriteParts(output, "ERR "u8, digits[first..], " "u8, response.PayloadBytes);
      }
      case ProtocolResponseKind.Data:
        ProtocolEscaping.ValidateEncodedData(response.PayloadBytes);
        return WriteParts(output, "D "u8, response.PayloadBytes);
      case ProtocolResponseKind.Status:
        return WriteKeyword(output, "S "u8, response.KeywordBytes, response.Arguments);
      case ProtocolResponseKind.Inquire:
        return WriteKeyword(output, "INQUIRE "u8, response.KeywordBytes, response.Arguments);
      case ProtocolResponseKind.End:
        return WriteParts(output, "END"u8);
      case ProtocolResponseKind.Comment:
        return WriteParts(output, "#"u8, response.PayloadBytes);
      case ProtocolResponseKind.Empty:
        return WriteParts(output, ReadOnlySpan<byte>.Empty);
      default:
        throw new ProtocolException(ProtocolErrorKind.InvalidResponse);
    }
  }

  /// <summary>Encodes as much raw binary data as fits in one D line without splitting an escape.</summary>
  /// <returns>Number of input bytes consumed and wire bytes written, including LF.</returns>
  public static (int Consumed, int Written) EncodeDataChunk(ReadOnlySpan<byte> input, Span<byte> output) {
    ValidateOutput(output);
    output[0] = (byte)'D';
    output[1] = (byte)' ';
    var written = 2;
    var consumed = 0;
    foreach (var value in input) {
      var escape = value switch {
        (byte)'%' => "%25"u8,
        (byte)'\r' => "%0D"u8,
        (byte)'\n' => "%0A"u8,
        0 => "%00"u8,
        var _ => ReadOnlySpan<byte>.Empty
      };
      var cost = escape.IsEmpty ? 1 : 3;
      if ((written + cost) >= MAX_LINE_BYTES) {
        break;
      }

      if (escape.IsEmpty) {
        output[written++] = value;
      }
      else {
        escape.CopyTo(output[written..]);
        written += 3;
      }

      ++consumed;
    }

    output[written++] = (byte)'\n';
    return (consumed, written);
  }

  private static int WriteKeyword(Span<byte> output, ReadOnlySpan<byte> prefix,
  ReadOnlySpan<byte> keyword, ReadOnlySpan<byte> args) {
    ProtocolFields.ValidateKeyword(keyword);

    return args.IsEmpty
      ? WriteParts(output, prefix, keyword)
      : WriteParts(output, prefix, keyword, " "u8, args);
  }

  private static void ValidateOutput(Span<byte> output) {
    if (output.Length < MAX_LINE_BYTES) {
      throw new ArgumentException("A complete protocol output buffer requires 1,000 bytes.", nameof(output));
    }
  }

  private static int WriteParts(Span<byte> output, ReadOnlySpan<byte> first,
  ReadOnlySpan<byte> second = default, ReadOnlySpan<byte> third = default, ReadOnlySpan<byte> fourth = default) {
    ProtocolFields.ValidateLine(first);
    ProtocolFields.ValidateLine(second);
    ProtocolFields.ValidateLine(third);
    ProtocolFields.ValidateLine(fourth);
    var total = 1L + first.Length + second.Length + third.Length + fourth.Length;
    if (total > MAX_LINE_BYTES) {
      throw new ProtocolException(ProtocolErrorKind.LineTooLong);
    }

    var position = 0;
    first.CopyTo(output[position..]);
    position += first.Length;
    second.CopyTo(output[position..]);
    position += second.Length;
    third.CopyTo(output[position..]);
    position += third.Length;
    fourth.CopyTo(output[position..]);
    position += fourth.Length;
    output[position] = (byte)'\n';

    return (int)total;
  }
}
