// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.SExpressions;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.SExpressions;

public sealed class CompatibilityTests {
  [Fact]
  public void CanonicalVectorsMatchTheRustLibrary() {
    byte[][] vectors = [
      "0:"u8.ToArray(),
      "()"u8.ToArray(),
      "(3:foo0:)"u8.ToArray(),
      "((1:a)(1:b))"u8.ToArray(),
      [(byte)'(', (byte)'3', (byte)':', 0, 0xff, (byte)')', (byte)'0', (byte)':', (byte)')']
    ];
    foreach (var vector in vectors) {
      var parsed = SExpressionParser.ParseComplete(vector);
      Assert.True(SExpressionWriter.ToCanonical(parsed).AsSpan().SequenceEqual(vector));
      Assert.True(SExpressionWriter.ToCanonical(parsed.ToOwned()).AsSpan().SequenceEqual(vector));
    }
  }

  [Fact]
  public void DeterministicBinaryRoundTripsDoNotTreatAtomBytesAsSyntax() {
    var random = new Random(2026);
    for (var index = 0; index < 128; ++index) {
      var bytes = new byte[index];
      random.NextBytes(bytes);
      var original = SExpression.List([SExpression.Atom(bytes), SExpression.List([])]);
      var canonical = SExpressionWriter.ToCanonical(original);
      var parsed = SExpressionParser.ParseComplete(canonical);
      Assert.Equal(canonical, SExpressionWriter.ToCanonical(parsed.ToOwned()));
    }
  }

  [Fact]
  public void SuffixAndSensitiveContentNeverAppearInDiagnostics() {
    var input = "(9:superkey!x)"u8;
    var error = ParserTests.Error(input);
    Assert.Equal(SExpressionParseErrorKind.UnexpectedToken, error.Kind);
    Assert.DoesNotContain("superkey", error.Message);
    Assert.DoesNotContain("superkey", error.ToString());

    var owned = SExpressionParser.ParseComplete("(9:superkey!)"u8).ToOwned();
    Assert.DoesNotContain("superkey", owned.ToString());
    var output = new List<byte>(new byte[SExpressionLimits.Default.MaxInputBytes]);
    var encodingError = Assert.Throws<SExpressionEncodeException>(() => SExpressionWriter.AppendCanonical(owned, output));
    Assert.DoesNotContain("superkey", encodingError.Message);
  }

  [Fact]
  public void PrefixesCanBeParsedInSequenceWithTheOriginalInputLimit() {
    var input = "(1:a)1:b0:"u8.ToArray();
    var limits = new SExpressionLimits(input.Length, input.Length, 8, 8);
    var remaining = input.AsSpan();
    var count = 0;
    while (!remaining.IsEmpty) {
      var expression = SExpressionParser.ParsePrefix(remaining, limits, out var consumed);
      Assert.True(SExpressionWriter.ToCanonical(expression).AsSpan().SequenceEqual(remaining[..consumed]));
      remaining = remaining[consumed..];
      ++count;
    }

    Assert.Equal(3, count);
  }

  [Fact]
  public void AnAtomAtDefaultMaximumNeedsRoomForItsLengthPrefix() {
    var input = new byte[SExpressionLimits.Default.MaxAtomBytes];
    Array.Fill(input, (byte)'z');
    var atom = SExpression.Atom(input);
    try {
      SExpressionWriter.ToCanonical(atom);
    }
    catch (SExpressionEncodeException error) {
      Assert.Equal(SExpressionEncodeErrorKind.SizeLimit, error.Kind);
      return;
    }

    throw new XunitException("Expected a size failure due to the decimal prefix.");
  }
}
