// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Expressions;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Expressions;

[TestSubject(typeof(SymbolicExpressionParser))]
public sealed class SymbolicExpressionParserTests {
  [Test]
  public void Parse_LengthPrefixedAtom_ShouldSucceed() {
    var response = new AssuanResponse("D 10:public-key"u8.ToArray());
    var expr = SymbolicExpressionParser.Parse(response, out var consumed);
    var expectedAtom = "public-key"u8.ToArray();

    expr.Type.ShouldBe(SymbolicExpressionType.Atom);
    expr.ShouldBeOfType<SymbolicExpressionAtom>();
    ((SymbolicExpressionAtom)expr).Value.ShouldBeEquivalentTo(expectedAtom);
    consumed.ShouldBe(response.Buffer.Length);
  }

  [Test]
  public void Parse_SimpleList_ShouldReturnCollection() {
    var response = new AssuanResponse("D (7:sig-val(3:rsa(1:s8:#010203#)))"u8.ToArray());
    var expr = SymbolicExpressionParser.Parse(response, out var consumed);

    expr.Type.ShouldBe(SymbolicExpressionType.Collection);
    expr.ShouldBeOfType<SymbolicExpressionCollection>();

    var exprColl = (SymbolicExpressionCollection)expr;
    exprColl.Children.Count.ShouldBe(2);
    exprColl.Children.First().Type.ShouldBe(SymbolicExpressionType.Atom);
    exprColl.Children.ElementAt(1).Type.ShouldBe(SymbolicExpressionType.Collection);
    consumed.ShouldBe(response.Buffer.Length);
  }

  [Test]
  public void Parse_NestedListWithBinaryData_ShouldParseCorrectly() {
    var response = new AssuanResponse("D (7:enc-val(3:rsa(1:a16:A1B2C3D4E5F6G7H8)(1:b4:\x41\x42\x43\x53)))"u8.ToArray());
    var expr = SymbolicExpressionParser.Parse(response, out var _);

    var top = (SymbolicExpressionCollection)expr;
    top.Children.Count.ShouldBe(2);

    var encVal = (SymbolicExpressionAtom)top.Children.First();
    Encoding.UTF8.GetString(encVal.Value).ShouldBe("enc-val");

    var rsaList = (SymbolicExpressionCollection)top.Children.Skip(1).First();
    rsaList.Children.Count.ShouldBe(2);

    var aList = (SymbolicExpressionCollection)rsaList.Children.Skip(1).First();
    aList.Children.Count.ShouldBe(3);

    var aAtom = (SymbolicExpressionAtom)aList.Children.Skip(1).First();
    aAtom.Value.Length.ShouldBe(16);

    var bList = (SymbolicExpressionCollection)aList.Children.Skip(2).First();
    bList.Children.Count.ShouldBe(2);

    var bAtom = (SymbolicExpressionAtom)bList.Children.Skip(1).First();
    Encoding.UTF8.GetString(bAtom.Value).ShouldBe("ABCS");
  }

  [Test]
  public void Parse_NonDataResponse_ShouldThrow() {
    var response = new AssuanResponse("OK"u8.ToArray());

    Should.Throw<InvalidAssuanResponseTypeException>(() => SymbolicExpressionParser.Parse(response, out var _));
  }

  [Test]
  public void Parse_AtomLengthTooLarge_ShouldThrow() {
    var response = new AssuanResponse("D 2:x"u8.ToArray());

    Should.Throw<AtomLengthOutOfRangeException>(() => SymbolicExpressionParser.Parse(response, out var _));
  }

  [Test]
  public void Parse_MissingColonAfterLength_ShouldThrow() {
    var response = new AssuanResponse("D 6foobar"u8.ToArray());

    Should.Throw<InvalidSymbolicExpressionSyntaxException>(() => SymbolicExpressionParser.Parse(response, out var _));
  }

  [Test]
  public void Parse_UnclosedList_ShouldThrow() {
    var response = new AssuanResponse("D (4:open(12:nested_thing"u8.ToArray());

    Should.Throw<IncompleteSymbolicExpressionException>(() => SymbolicExpressionParser.Parse(response, out var _));
  }

  [Test]
  public void TryParse_InvalidInput_ShouldReturnFalse() {
    var response = new AssuanResponse("D (6:broken"u8.ToArray());
    var success = SymbolicExpressionParser.TryParse(response, out var expr, out var consumed);

    success.ShouldBeFalse();
    expr.ShouldBeNull();
    consumed.ShouldBe(9);
  }

  [Test]
  public void Parse_ExtraWhitespace_ShouldBeIgnored() {
    var response = new AssuanResponse("D    \t\n  13:hello   world  "u8.ToArray());

    var expr = SymbolicExpressionParser.Parse(response, out var _);

    Encoding.UTF8.GetString(((SymbolicExpressionAtom)expr).Value).ShouldBe("hello   world");
  }

  [Test]
  public void Parse_EmptyDataResponse_ShouldThrow() {
    var response = new AssuanResponse("D "u8.ToArray());

    Should.Throw<IncompleteSymbolicExpressionException>(() => SymbolicExpressionParser.Parse(response, out var _));
  }

  [Test]
  public async Task Parse_JustParentheses_ShouldReturnEmptyCollection() {
    var response = new AssuanResponse("D ()"u8.ToArray());

    var expr = SymbolicExpressionParser.Parse(response, out var consumed);

    var coll = (SymbolicExpressionCollection)expr;
    await Assert.That(coll.Children).IsEmpty();
    await Assert.That(consumed).IsEqualTo(2);
  }
}
