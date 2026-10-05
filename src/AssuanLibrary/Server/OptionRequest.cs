// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Server;

/// <summary>Validated, borrowed OPTION name and optionally present wire value.</summary>
public readonly struct OptionRequest {
  /// <summary>ASCII option token without an optional leading --.</summary>
  public ReadOnlyMemory<byte> Name { get; }

  /// <summary>Literal wire bytes; null when no value was supplied, empty when explicitly present.</summary>
  public ReadOnlyMemory<byte>? Value { get; }

  private OptionRequest(ReadOnlyMemory<byte> name, ReadOnlyMemory<byte>? value) {
    Name = name;
    Value = value;
  }

  /// <summary>Parses one OPTION argument without decoding percent sequences or copying payload bytes.</summary>
  public static OptionRequest Parse(ReadOnlyMemory<byte> bytes) {
    ProtocolFields.ValidateLine(bytes.Span);
    bytes = Trim(bytes);
    if (bytes.Span.StartsWith("--"u8)) {
      bytes = bytes[2..];
    }

    var end = bytes.Span.IndexOfAny((byte)' ', (byte)'\t', (byte)'=');
    if (end < 0) {
      end = bytes.Length;
    }

    var name = bytes[..end];
    ProtocolFields.ValidateToken(name.Span);
    var rest = Trim(bytes[end..]);
    ReadOnlyMemory<byte>? value = null;
    if (rest.Span.StartsWith("="u8)) {
      value = Trim(rest[1..]);
    }
    else if (!rest.IsEmpty) {
      value = rest;
    }

    return new OptionRequest(name, value);
  }

  /// <inheritdoc />
  public override string ToString()
    => $"OptionRequest {{ NameLength = {Name.Length}, HasValue = {Value.HasValue} }}";

  private static ReadOnlyMemory<byte> Trim(ReadOnlyMemory<byte> source) {
    var start = 0;
    var end = source.Length;
    while (start < end &&
           IsSpace(source.Span[start])) {
      ++start;
    }

    while (end > start &&
           IsSpace(source.Span[end - 1])) {
      --end;
    }

    return source[start..end];
  }

  private static bool IsSpace(byte value)
    => value is 9 or 10 or 11 or 12 or 13 or 32;
}
