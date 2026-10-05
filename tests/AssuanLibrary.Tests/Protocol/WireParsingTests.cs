// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.Protocol;

public sealed class WireParsingTests {
  [Fact]
  public void CommandsBorrowFieldsWithoutNormalizingOrDecoding() {
    byte[] line = [
      (byte)'X', (byte)'_', (byte)'C', (byte)'M', (byte)'D', (byte)' ',
      (byte)' ', (byte)'%', (byte)'G', (byte)'G', (byte)' ', 0xff
    ];
    var command = ProtocolCommand.Parse(line);
    Assert.True(command.NameBytes.SequenceEqual("X_CMD"u8));
    Assert.True(command.Arguments.SequenceEqual(line.AsSpan(6)));
    line[^1] = 3;
    Assert.Equal((byte)3, command.Arguments[^1]);
    var built = ProtocolCommand.Create(command.NameBytes, command.Arguments);
    Assert.True(built.Arguments.SequenceEqual(command.Arguments));
    Assert.True(ProtocolCommand.Parse("NOP "u8).Arguments.IsEmpty);
  }

  [Theory]
  [InlineData("", ProtocolErrorKind.InvalidToken)]
  [InlineData(" GETINFO", ProtocolErrorKind.InvalidToken)]
  [InlineData("#comment", ProtocolErrorKind.InvalidToken)]
  [InlineData("%41", ProtocolErrorKind.InvalidToken)]
  [InlineData("X\tY", ProtocolErrorKind.InvalidToken)]
  [InlineData("X\rY", ProtocolErrorKind.InvalidLine)]
  [InlineData("X\nY", ProtocolErrorKind.InvalidLine)]
  [InlineData("X\0Y", ProtocolErrorKind.InvalidLine)]
  public void RejectsInvalidCommands(string text, ProtocolErrorKind kind) => Assert.Equal(kind, CommandError(Encoding.UTF8.GetBytes(text)).Kind);

  [Theory]
  [InlineData("OKAY", ProtocolErrorKind.InvalidResponse)]
  [InlineData("D", ProtocolErrorKind.InvalidResponse)]
  [InlineData("END ", ProtocolErrorKind.InvalidResponse)]
  [InlineData("S 9FOO", ProtocolErrorKind.InvalidToken)]
  [InlineData("INQUIRE  foo", ProtocolErrorKind.InvalidToken)]
  [InlineData("ERR", ProtocolErrorKind.InvalidResponse)]
  [InlineData("ERR ", ProtocolErrorKind.InvalidErrorCode)]
  [InlineData("ERR  1", ProtocolErrorKind.InvalidErrorCode)]
  [InlineData("ERR 4294967296", ProtocolErrorKind.InvalidErrorCode)]
  [InlineData("OK\r\n", ProtocolErrorKind.InvalidLine)]
  public void RejectsMalformedResponses(string text, ProtocolErrorKind kind) => Assert.Equal(kind, ResponseError(Encoding.UTF8.GetBytes(text)).Kind);

  [Fact]
  public void ParsesAllServerCategoriesWithOriginalBinaryFields() {
    Assert.Equal(ProtocolResponseKind.Empty, ProtocolResponse.Parse(ReadOnlySpan<byte>.Empty).Kind);
    Assert.Equal(ProtocolResponseKind.End, ProtocolResponse.Parse("END"u8).Kind);
    Assert.True(ProtocolResponse.Parse("#  note"u8).PayloadBytes.SequenceEqual("  note"u8));
    Assert.True(ProtocolResponse.Parse("OK  ready"u8).PayloadBytes.SequenceEqual(" ready"u8));

    var error = ProtocolResponse.Parse("ERR 4294967295  failed "u8);
    Assert.Equal(ProtocolResponseKind.Err, error.Kind);
    Assert.Equal(uint.MaxValue, error.ErrorCode);
    Assert.True(error.PayloadBytes.SequenceEqual(" failed "u8));
    Assert.Equal((uint)42, ProtocolResponse.Parse("ERR 00042"u8).ErrorCode);

    byte[] data = [(byte)'D', (byte)' ', (byte)' ', (byte)'%', (byte)'G', (byte)'G', 0xff];
    Assert.True(ProtocolResponse.Parse(data).PayloadBytes.SequenceEqual(data.AsSpan(2)));
    byte[] status = [(byte)'S', (byte)' ', (byte)'_', (byte)'X', (byte)' ', (byte)' ', 0xff];
    var parsedStatus = ProtocolResponse.Parse(status);
    Assert.Equal(ProtocolResponseKind.Status, parsedStatus.Kind);
    Assert.True(parsedStatus.KeywordBytes.SequenceEqual("_X"u8));
    Assert.True(parsedStatus.Arguments.SequenceEqual(status.AsSpan(5)));
    Assert.True(ProtocolResponse.Parse("INQUIRE PIN"u8).Arguments.IsEmpty);
  }

  [Fact]
  public void NeitherErrorsNorDiagnosticsEchoReceivedBytes() {
    var error = ResponseError("D secret\0"u8);
    Assert.DoesNotContain("secret", error.Message);
    Assert.DoesNotContain("secret", error.ToString());
    Assert.DoesNotContain("secret", ProtocolResponse.Parse("S SECRET secret"u8).ToString());
    Assert.DoesNotContain("secret", ProtocolCommand.Parse("SECRET secret"u8).ToString());
  }

  internal static ProtocolException CommandError(ReadOnlySpan<byte> line) {
    try { ProtocolCommand.Parse(line); }
    catch (ProtocolException error) { return error; }

    throw new XunitException("Expected an invalid command.");
  }

  internal static ProtocolException ResponseError(ReadOnlySpan<byte> line) {
    try { ProtocolResponse.Parse(line); }
    catch (ProtocolException error) { return error; }

    throw new XunitException("Expected an invalid response.");
  }
}
