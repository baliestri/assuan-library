// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.SExpressions;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.SExpressions;

public sealed class BoundaryTests {
  [Fact]
  public void ChecksInputLimitBeforeExaminingEvenAValidPrefix() {
    var limits = new SExpressionLimits(4, 2, 4, 4);
    Assert.True(SExpressionParser.ParseComplete("2:ab"u8, limits).IsAtom);
    try {
      SExpressionParser.ParsePrefix("0:abc"u8, limits, out var _);
    }
    catch (SExpressionParseException error) {
      Assert.Equal(SExpressionParseErrorKind.InputLimit, error.Kind);
      Assert.Equal(4, error.Offset);
      return;
    }

    throw new XunitException("Expected an input limit failure.");
  }

  [Fact]
  public void ChecksDeclaredAtomLimitBeforeMissingPayload() {
    var limits = new SExpressionLimits(32, 3, 8, 8);
    var error = ParserTests.Error("4:"u8, limits);
    Assert.Equal(SExpressionParseErrorKind.AtomLimit, error.Kind);
    Assert.Equal(0, error.Offset);
  }

  [Fact]
  public void CountsListsAndAtomsAsNodes() {
    var limits = new SExpressionLimits(64, 64, 3, 8);
    Assert.False(SExpressionParser.ParseComplete("(1:a1:b)"u8, limits).IsAtom);
    Assert.False(SExpressionParser.ParseComplete("(()())"u8, limits).IsAtom);
    var error = ParserTests.Error("(1:a1:b0:)"u8, limits);
    Assert.Equal(SExpressionParseErrorKind.NodeLimit, error.Kind);
    Assert.Equal(7, error.Offset);
  }

  [Fact]
  public void Accepts64LevelsByDefaultAndRejects65() {
    var valid = Encoding.ASCII.GetBytes(new string('(', 64) + "0:" + new string(')', 64));
    Assert.False(SExpressionParser.ParseComplete(valid).IsAtom);
    var invalid = Encoding.ASCII.GetBytes(new string('(', 65) + "0:" + new string(')', 65));
    var error = ParserTests.Error(invalid);
    Assert.Equal(SExpressionParseErrorKind.DepthLimit, error.Kind);
    Assert.Equal(64, error.Offset);
  }

  [Fact]
  public void ExtremelyDeepTruncatedInputFailsAtDepthNotAtEndOfInput() {
    var input = new byte[100_000];
    Array.Fill(input, (byte)'(');
    var error = ParserTests.Error(input);
    Assert.Equal(SExpressionParseErrorKind.DepthLimit, error.Kind);
    Assert.Equal(64, error.Offset);
  }
}
