// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.SExpressions;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.SExpressions;

public sealed class WriterTests {
  [Fact]
  public void EncodesBinaryTreeWithoutWhitespaceOrTextTranscoding() {
    var value = SExpression.List([
      SExpression.Atom([(byte)'(', 0, (byte)')', 0xff]),
      SExpression.List([]),
      SExpression.Atom(" "u8)
    ]);
    byte[] expected = [
      (byte)'(', (byte)'4', (byte)':', (byte)'(', 0, (byte)')', 0xff,
      (byte)'(', (byte)')', (byte)'1', (byte)':', (byte)' ', (byte)')'
    ];
    Assert.Equal(expected, SExpressionWriter.ToCanonical(value));
    Assert.Equal(expected, SExpressionWriter.ToCanonical(SExpressionParser.ParseComplete(expected)));
  }

  [Fact]
  public void Utf8ByteLengthAndDecimalBoundaries() {
    Assert.Equal(new byte[] { (byte)'2', (byte)':', 0xc3, 0xa9 },
      SExpressionWriter.ToCanonical(SExpression.Atom("é"u8)));
    foreach (var length in new[] { 0, 1, 9, 10, 99, 100, 999, 1000 }) {
      var atom = new byte[length];
      Array.Fill(atom, (byte)0xff);
      var wire = SExpressionWriter.ToCanonical(SExpression.Atom(atom));
      Assert.True(wire.AsSpan().StartsWith(Encoding.ASCII.GetBytes(length + ":")));
      Assert.True(SExpressionParser.ParseComplete(wire).AtomBytes.SequenceEqual(atom));
    }
  }

  [Fact]
  public void AppendPreservesExistingPrefix() {
    var output = new List<byte>("prefix:"u8.ToArray());
    SExpressionWriter.AppendCanonical(SExpression.Atom([]), output);
    SExpressionWriter.AppendCanonical(SExpression.List([]), output);
    Assert.Equal("prefix:0:()"u8.ToArray(), output);
    SExpressionWriter.AppendCanonical(SExpressionParser.ParseComplete("1:a"u8), output);
    Assert.Equal("prefix:0:()1:a"u8.ToArray(), output);
  }

  [Fact]
  public void RejectsExcessiveDepthAndNodesWithoutAppending() {
    var deep = SExpression.Atom("x"u8);
    for (var index = 0; index < 65; ++index) {
      deep = SExpression.List([deep]);
    }

    var output = new List<byte>("keep"u8.ToArray());
    var error = EncodeError(SExpression.List([SExpression.Atom("valid-first"u8), deep]), output);
    Assert.Equal(SExpressionEncodeErrorKind.DepthLimit, error.Kind);
    Assert.Equal("keep"u8.ToArray(), output);

    var wide = SExpression.List(Enumerable.Repeat(SExpression.Atom([]), 65_536));
    Assert.Equal(SExpressionEncodeErrorKind.NodeLimit, EncodeError(wide).Kind);
  }

  [Fact]
  public void ExistingOutputAndLengthPrefixCountTowardSizeLimit() {
    var output = new List<byte>(new byte[SExpressionLimits.Default.MaxInputBytes]);
    for (var index = 0; index < output.Count; ++index) {
      output[index] = (byte)'x';
    }

    Assert.Equal(SExpressionEncodeErrorKind.SizeLimit, EncodeError(SExpression.Atom([]), output).Kind);
    Assert.Equal(SExpressionLimits.Default.MaxInputBytes, output.Count);
    Assert.True(output.TrueForAll(value => value == (byte)'x'));

    var atom = new byte[SExpressionLimits.Default.MaxAtomBytes];
    Assert.Equal(SExpressionEncodeErrorKind.SizeLimit, EncodeError(SExpression.Atom(atom)).Kind);
  }

  [Fact]
  public void RejectsOversizedAtomAndCustomDepthViewUnderDefaultWriterLimits() {
    var huge = SExpression.Atom(new byte[SExpressionLimits.Default.MaxAtomBytes + 1]);
    var output = new List<byte>("keep"u8.ToArray());
    Assert.Equal(SExpressionEncodeErrorKind.AtomLimit, EncodeError(huge, output).Kind);
    Assert.Equal("keep"u8.ToArray(), output);

    var input = Encoding.ASCII.GetBytes(new string('(', 65) + "0:" + new string(')', 65));
    var value = SExpressionParser.ParseComplete(input, new SExpressionLimits(1024, 1024, 128, 256));
    try {
      SExpressionWriter.AppendCanonical(value, output);
    }
    catch (SExpressionEncodeException error) {
      Assert.Equal(SExpressionEncodeErrorKind.DepthLimit, error.Kind);
      Assert.Equal("keep"u8.ToArray(), output);
      return;
    }

    throw new XunitException("Expected a writer depth failure.");
  }

  [Fact]
  public void CustomNodeBudgetCanParseButDefaultWriterStillRejectsTheView() {
    var encoded = new byte[(65_536 * 2) + 2];
    encoded[0] = (byte)'(';
    for (var index = 0; index < 65_536; ++index) {
      encoded[1 + (index * 2)] = (byte)'0';
      encoded[2 + (index * 2)] = (byte)':';
    }

    encoded[^1] = (byte)')';
    var limits = new SExpressionLimits(encoded.Length, encoded.Length, 65_537, 64);
    var view = SExpressionParser.ParseComplete(encoded, limits);
    var output = new List<byte>("keep"u8.ToArray());
    try {
      SExpressionWriter.AppendCanonical(view, output);
    }
    catch (SExpressionEncodeException error) {
      Assert.Equal(SExpressionEncodeErrorKind.NodeLimit, error.Kind);
      Assert.Equal("keep"u8.ToArray(), output);
      return;
    }

    throw new XunitException("Expected the default writer node limit.");
  }

  private static SExpressionEncodeException EncodeError(SExpression value, List<byte>? output = null) {
    try {
      if (output is null) {
        SExpressionWriter.ToCanonical(value);
      }
      else {
        SExpressionWriter.AppendCanonical(value, output);
      }
    }
    catch (SExpressionEncodeException error) {
      return error;
    }

    throw new XunitException("Expected an encoding failure.");
  }
}
