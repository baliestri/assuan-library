// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>A validated command borrowing the original wire name and argument bytes.</summary>
/// <remarks>Input excludes LF/CRLF; keep both buffers stable while using this scoped view.</remarks>
public readonly ref struct ProtocolCommand {
  /// <summary>The validated case-preserving ASCII command name.</summary>
  public ReadOnlySpan<byte> NameBytes { get; }

  /// <summary>The original binary wire arguments, with exactly the first separator removed.</summary>
  public ReadOnlySpan<byte> Arguments { get; }

  private ProtocolCommand(ReadOnlySpan<byte> name, ReadOnlySpan<byte> arguments) {
    NameBytes = name;
    Arguments = arguments;
  }

  /// <summary>Parses a line without its LF/CRLF terminator, without copying the fields.</summary>
  public static ProtocolCommand Parse(ReadOnlySpan<byte> line) {
    ProtocolFields.ValidateLine(line);
    ProtocolFields.Split(line, out var name, out var arguments);
    ProtocolFields.ValidateToken(name);

    return new ProtocolCommand(name, arguments);
  }

  /// <summary>Validates already separated name and wire arguments without copying them.</summary>
  public static ProtocolCommand Create(ReadOnlySpan<byte> name, ReadOnlySpan<byte> arguments) {
    ProtocolFields.ValidateToken(name);
    ProtocolFields.ValidateLine(arguments);

    return new ProtocolCommand(name, arguments);
  }

  /// <inheritdoc />
  public override string ToString()
    => $"Command {{ NameLength = {NameBytes.Length}, ArgumentsLength = {Arguments.Length} }}";
}
