// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.SExpressions;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.SExpressions;

public sealed class LimitsTests {
  [Fact]
  public void DefaultsAndSupportedBoundaries() {
    var limits = SExpressionLimits.Default;
    Assert.Equal(16 * 1024 * 1024, limits.MaxInputBytes);
    Assert.Equal(16 * 1024 * 1024, limits.MaxAtomBytes);
    Assert.Equal(65_536, limits.MaxNodes);
    Assert.Equal(64, limits.MaxDepth);
    Assert.Equal(256, SExpressionLimits.MAX_NESTING_DEPTH);

    var restricted = new SExpressionLimits(4, 0, 1, 0);
    Assert.Equal(0, restricted.MaxDepth);
    Assert.True(SExpressionParser.ParseComplete("0:"u8, restricted).IsAtom);
    Assert.Equal(SExpressionParseErrorKind.DepthLimit, Error("()"u8, restricted).Kind);

    var maximum = new SExpressionLimits(1024, 1024, 257, 256);
    Assert.False(SExpressionParser.ParseComplete(
      Encoding.ASCII.GetBytes(new string('(', 256) + "0:" + new string(')', 256)), maximum).IsAtom);
  }

  [Theory]
  [InlineData(0, 0, 1, 0)]
  [InlineData(1, -1, 1, 0)]
  [InlineData(1, 2, 1, 0)]
  [InlineData(1, 0, 0, 0)]
  [InlineData(1, 0, 1, -1)]
  [InlineData(1, 0, 1, 257)]
  public void RejectsInvalidLimitsWithoutInventingAnOffset(int input, int atom, int nodes, int depth) {
    var error = Assert.Throws<SExpressionParseException>(() => new SExpressionLimits(input, atom, nodes, depth));
    Assert.Equal(SExpressionParseErrorKind.InvalidLimits, error.Kind);
    Assert.Null(error.Offset);
  }

  private static SExpressionParseException Error(ReadOnlySpan<byte> input, SExpressionLimits limits) {
    try {
      SExpressionParser.ParseComplete(input, limits);
    }
    catch (SExpressionParseException error) {
      return error;
    }

    throw new XunitException("Expected a parsing failure.");
  }
}
