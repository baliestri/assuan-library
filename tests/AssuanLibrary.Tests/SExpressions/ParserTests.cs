// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.SExpressions;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.SExpressions;

public sealed class ParserTests {
  [Fact]
  public void BorrowsBinaryAtomsIncludingExpressionSyntax() {
    byte[] input = [(byte)'3', (byte)':', 0, 0xff, (byte)')'];
    var value = SExpressionParser.ParseComplete(input);
    Assert.True(value.IsAtom);
    Assert.True(value.AtomBytes.SequenceEqual(input.AsSpan(2)));
    input[3] = 7;
    Assert.Equal((byte)7, value.AtomBytes[1]);
  }

  [Fact]
  public void PrefixConsumesOneExpressionButDoesNotValidateTheSuffix() {
    var first = SExpressionParser.ParsePrefix("(1:a)1:b"u8, out var consumed);
    Assert.False(first.IsAtom);
    Assert.Equal(5, consumed);
    Assert.True(SExpressionParser.ParseComplete("1:b"u8).AtomBytes.SequenceEqual("b"u8));
    SExpressionParser.ParsePrefix("0:garbage"u8, out consumed);
    Assert.Equal(2, consumed);
  }

  [Theory]
  [InlineData("", SExpressionParseErrorKind.UnexpectedEof, 0)]
  [InlineData("(", SExpressionParseErrorKind.UnexpectedEof, 1)]
  [InlineData("(0:", SExpressionParseErrorKind.UnexpectedEof, 3)]
  [InlineData("3:ab", SExpressionParseErrorKind.UnexpectedEof, 4)]
  [InlineData("1", SExpressionParseErrorKind.UnexpectedEof, 1)]
  [InlineData("(1:a", SExpressionParseErrorKind.UnexpectedEof, 4)]
  [InlineData("((1:a)", SExpressionParseErrorKind.UnexpectedEof, 6)]
  [InlineData("01:a", SExpressionParseErrorKind.InvalidLength, 1)]
  [InlineData("0001:a", SExpressionParseErrorKind.InvalidLength, 1)]
  [InlineData("1;a", SExpressionParseErrorKind.ExpectedColon, 1)]
  [InlineData(" ", SExpressionParseErrorKind.UnexpectedToken, 0)]
  [InlineData("( )", SExpressionParseErrorKind.UnexpectedToken, 1)]
  [InlineData("0: ", SExpressionParseErrorKind.TrailingData, 2)]
  [InlineData("0:0:", SExpressionParseErrorKind.TrailingData, 2)]
  [InlineData("0:)", SExpressionParseErrorKind.TrailingData, 2)]
  public void MatchesRustSyntaxErrorKindsAndOffsets(string text, SExpressionParseErrorKind kind, int offset) {
    var error = Error(Encoding.UTF8.GetBytes(text));
    Assert.Equal(kind, error.Kind);
    Assert.Equal(offset, error.Offset);
  }

  [Fact]
  public void EmptyAtomAndListAreValidWithoutTrimming() {
    Assert.Empty(SExpressionParser.ParseComplete("0:"u8).AtomBytes.ToArray());
    Assert.False(SExpressionParser.ParseComplete("()"u8).IsAtom);
    Assert.Equal(SExpressionParseErrorKind.UnexpectedToken, Error("\t()"u8).Kind);
  }

  [Fact]
  public void DiagnosesLengthOverflowAtTheDigitNotAtThePayload() {
    var error = Error("184467440737095516150:"u8);
    Assert.Equal(SExpressionParseErrorKind.LengthOverflow, error.Kind);
    Assert.Equal(20, error.Offset);
    Assert.Equal(SExpressionParseErrorKind.AtomLimit, Error("2147483648:"u8).Kind);
  }

  internal static SExpressionParseException Error(ReadOnlySpan<byte> input, SExpressionLimits? limits = null) {
    try {
      SExpressionParser.ParseComplete(input, limits);
    }
    catch (SExpressionParseException error) {
      return error;
    }

    throw new XunitException("Expected a parsing failure.");
  }
}
