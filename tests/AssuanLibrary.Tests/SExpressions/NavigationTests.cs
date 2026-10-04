// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.SExpressions;
using Xunit.Sdk;

namespace AssuanLibrary.Tests.SExpressions;

public sealed class NavigationTests {
  [Fact]
  public void TraversesNestedSiblingsWithoutMergingTheirParents() {
    var root = SExpressionParser.ParseComplete("((1:a)(1:b))"u8);
    var parents = root.Children.GetEnumerator();
    Assert.True(parents.MoveNext());
    var first = parents.Current.Children.GetEnumerator();
    Assert.True(first.MoveNext());
    Assert.True(first.Current.AtomBytes.SequenceEqual("a"u8));
    Assert.False(first.MoveNext());

    Assert.True(parents.MoveNext());
    var second = parents.Current.Children.GetEnumerator();
    Assert.True(second.MoveNext());
    Assert.True(second.Current.AtomBytes.SequenceEqual("b"u8));
    Assert.False(second.MoveNext());
    Assert.False(parents.MoveNext());
  }

  [Fact]
  public void BinaryAtomContainingClosingParenthesisDoesNotEndTheList() {
    byte[] input = [(byte)'(', (byte)'3', (byte)':', 0, 0xff, (byte)')', (byte)'0', (byte)':', (byte)')'];
    var children = SExpressionParser.ParseComplete(input).Children.GetEnumerator();
    Assert.True(children.MoveNext());
    Assert.True(children.Current.AtomBytes.SequenceEqual(input.AsSpan(3, 3)));
    Assert.True(children.MoveNext());
    Assert.True(children.Current.AtomBytes.IsEmpty);
    Assert.False(children.MoveNext());
  }

  [Fact]
  public void EmptyListHasNoChildrenAndAtomHasNoChildren() {
    Assert.False(SExpressionParser.ParseComplete("()"u8).Children.GetEnumerator().MoveNext());
    try {
      SExpressionParser.ParseComplete("1:a"u8).Children.GetEnumerator();
    }
    catch (InvalidOperationException) {
      return;
    }

    throw new XunitException("An atom must not expose list children.");
  }

  [Fact]
  public void CustomDepthBeyondDefaultStillNavigatesWithoutReapplyingDefaultLimits() {
    var input = Encoding.ASCII.GetBytes(new string('(', 65) + "0:" + new string(')', 65));
    var root = SExpressionParser.ParseComplete(input, new SExpressionLimits(1024, 1024, 128, 256));
    for (var index = 0; index < 65; ++index) {
      var children = root.Children.GetEnumerator();
      Assert.True(children.MoveNext());
      root = children.Current;
    }

    Assert.True(root.IsAtom);
    Assert.True(root.AtomBytes.IsEmpty);
  }
}
