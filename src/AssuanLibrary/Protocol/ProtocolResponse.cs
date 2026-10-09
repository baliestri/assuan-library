// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>Category of an Assuan server line, without data bytes.</summary>
public enum ProtocolResponseKind {
  /// <summary>Successful final response.</summary>
  Ok,

  /// <summary>Final response containing a complete remote error code.</summary>
  Err,

  /// <summary>Percent-encoded data line.</summary>
  Data,

  /// <summary>Nonfinal informational status line.</summary>
  Status,

  /// <summary>Nonfinal request for client data.</summary>
  Inquire,

  /// <summary>Partial end marker, not a final response.</summary>
  End,

  /// <summary>Comment line.</summary>
  Comment,

  /// <summary>Empty framed line.</summary>
  Empty
}

/// <summary>A borrowed response in wire representation; D payloads remain percent-encoded.</summary>
/// <remarks>Input excludes LF/CRLF and must remain stable while using this scoped view.</remarks>
public readonly ref struct ProtocolResponse {
  private readonly ReadOnlySpan<byte> _payload;
  private readonly ReadOnlySpan<byte> _keyword;
  private readonly ReadOnlySpan<byte> _arguments;
  private readonly uint _errorCode;

  /// <summary>The category of this server response.</summary>
  public ProtocolResponseKind Kind { get; }

  private ProtocolResponse(ProtocolResponseKind kind, ReadOnlySpan<byte> payload = default,
  ReadOnlySpan<byte> keyword = default, ReadOnlySpan<byte> arguments = default, uint errorCode = 0) {
    Kind = kind;
    _payload = payload;
    _keyword = keyword;
    _arguments = arguments;
    _errorCode = errorCode;
  }

  /// <summary>OK/ERR diagnostic, encoded D data or comment bytes without the category prefix.</summary>
  public ReadOnlySpan<byte> PayloadBytes => Kind is ProtocolResponseKind.Ok or
    ProtocolResponseKind.Err or ProtocolResponseKind.Data or ProtocolResponseKind.Comment
    ? _payload
    : throw new InvalidOperationException("This response has no payload field.");

  /// <summary>A validated ASCII keyword for a status or inquiry.</summary>
  public ReadOnlySpan<byte> KeywordBytes => Kind is ProtocolResponseKind.Status or ProtocolResponseKind.Inquire
    ? _keyword
    : throw new InvalidOperationException("This response has no keyword field.");

  /// <summary>Unmodified binary wire arguments for a status or inquiry.</summary>
  public ReadOnlySpan<byte> Arguments => Kind is ProtocolResponseKind.Status or ProtocolResponseKind.Inquire
    ? _arguments
    : throw new InvalidOperationException("This response has no argument field.");

  /// <summary>The complete unsigned remote error code, including source bits.</summary>
  public uint ErrorCode => Kind == ProtocolResponseKind.Err
    ? _errorCode
    : throw new InvalidOperationException("This response has no error code.");

  /// <summary>Parses a server line without allocation or percent-decoding its data.</summary>
  public static ProtocolResponse Parse(ReadOnlySpan<byte> line) {
    ProtocolFields.ValidateLine(line);
    if (line.IsEmpty) {
      return Empty();
    }

    if (line[0] == (byte)'#') {
      return Comment(line[1..]);
    }

    if (line.SequenceEqual("OK"u8)) {
      return Ok(ReadOnlySpan<byte>.Empty);
    }

    if (line.StartsWith("OK "u8)) {
      return Ok(line[3..]);
    }

    if (line.StartsWith("ERR "u8)) {
      ProtocolFields.Split(line[4..], out var number, out var text);
      if (number.IsEmpty) {
        throw new ProtocolException(ProtocolErrorKind.InvalidErrorCode);
      }

      uint code = 0;
      foreach (var digit in number) {
        if (digit is < (byte)'0' or > (byte)'9' ||
            code > ((uint.MaxValue - (uint)(digit - (byte)'0')) / 10)) {
          throw new ProtocolException(ProtocolErrorKind.InvalidErrorCode);
        }

        code = (code * 10) + (uint)(digit - (byte)'0');
      }

      return Err(code, text);
    }

    if (line.StartsWith("D "u8)) {
      return Data(line[2..]);
    }

    if (line.StartsWith("S "u8)) {
      ProtocolFields.Split(line[2..], out var keyword, out var arguments);
      ProtocolFields.ValidateKeyword(keyword);
      return Status(keyword, arguments);
    }

    if (line.StartsWith("INQUIRE "u8)) {
      ProtocolFields.Split(line[8..], out var keyword, out var arguments);
      ProtocolFields.ValidateKeyword(keyword);
      return Inquire(keyword, arguments);
    }

    return line.SequenceEqual("END"u8)
      ? End()
      : throw new ProtocolException(ProtocolErrorKind.InvalidResponse);
  }

  /// <summary>Constructs an OK response with optional public diagnostic bytes.</summary>
  public static ProtocolResponse Ok(ReadOnlySpan<byte> text)
    => new(ProtocolResponseKind.Ok, text);

  /// <summary>Constructs an ERR response with its complete unsigned remote code.</summary>
  public static ProtocolResponse Err(uint code, ReadOnlySpan<byte> text)
    => new(ProtocolResponseKind.Err, text, errorCode: code);

  /// <summary>Constructs a D response whose payload is already percent-encoded wire data.</summary>
  public static ProtocolResponse Data(ReadOnlySpan<byte> wireData)
    => new(ProtocolResponseKind.Data, wireData);

  /// <summary>Constructs an informational status; fields are validated before encoding.</summary>
  public static ProtocolResponse Status(ReadOnlySpan<byte> keyword, ReadOnlySpan<byte> arguments)
    => new(ProtocolResponseKind.Status, keyword: keyword, arguments: arguments);

  /// <summary>Constructs a client-data request; fields are validated before encoding.</summary>
  public static ProtocolResponse Inquire(ReadOnlySpan<byte> keyword, ReadOnlySpan<byte> arguments)
    => new(ProtocolResponseKind.Inquire, keyword: keyword, arguments: arguments);

  /// <summary>Constructs a partial END response, not a final OK.</summary>
  public static ProtocolResponse End()
    => new(ProtocolResponseKind.End);

  /// <summary>Constructs a comment containing exactly the bytes after '#'.</summary>
  public static ProtocolResponse Comment(ReadOnlySpan<byte> comment)
    => new(ProtocolResponseKind.Comment, comment);

  /// <summary>Constructs an empty line.</summary>
  public static ProtocolResponse Empty()
    => new(ProtocolResponseKind.Empty);

  /// <inheritdoc />
  public override string ToString() => Kind switch {
    ProtocolResponseKind.Err => $"Err {{ Code = {_errorCode}, TextLength = {_payload.Length} }}",
    ProtocolResponseKind.Status or ProtocolResponseKind.Inquire =>
      $"{Kind} {{ KeywordLength = {_keyword.Length}, ArgumentsLength = {_arguments.Length} }}",
    var _ => $"{Kind} {{ Length = {_payload.Length} }}"
  };
}
