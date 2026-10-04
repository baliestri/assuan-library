// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.SExpressions;

namespace AssuanLibrary.Tests.SExpressions;

public sealed class OwnershipTests {
  [Fact]
  public void ToOwnedCopiesAtomsAndKeepsNestedStructure() {
    var input = "((3:key)0:())"u8.ToArray();
    var owned = SExpressionParser.ParseComplete(input).ToOwned();
    Array.Fill(input, (byte)'X');

    Assert.False(owned.IsAtom);
    Assert.Equal(3, owned.Children.Count);
    Assert.True(owned.Children[0].Children[0].AtomBytes.SequenceEqual("key"u8));
    Assert.True(owned.Children[1].AtomBytes.IsEmpty);
    Assert.Empty(owned.Children[2].Children);
    Assert.DoesNotContain("key", owned.ToString());
  }

  [Fact]
  public void FactoriesCopyCallerArraysAndChildCollections() {
    var source = "secret"u8.ToArray();
    var atom = SExpression.Atom(source);
    var children = new List<SExpression> { atom };
    var list = SExpression.List(children);
    source.AsSpan().Fill((byte)'X');
    children.Clear();

    var item = Assert.Single(list.Children);
    Assert.True(item.AtomBytes.SequenceEqual("secret"u8));
    Assert.DoesNotContain("secret", list.ToString());
    var exposed = Assert.IsType<IList<SExpression>>(list.Children, false);
    Assert.Throws<NotSupportedException>(() => exposed[0] = SExpression.Atom("changed"u8));
  }

  [Fact]
  public void RejectsNullChildrenWithoutPublishingThem() {
    Assert.Throws<ArgumentNullException>(() => SExpression.List(null!));
    Assert.Throws<ArgumentException>(() => SExpression.List([null!]));
  }

  [Fact]
  public async Task ScopedViewsAreConsumedBeforeAwaitWhileOwnedValuesCanSurvive() {
    static async Task<SExpression> ParseAndWaitAsync(byte[] bytes) {
      var owned = SExpressionParser.ParseComplete(bytes).ToOwned();
      await Task.Yield();
      return owned;
    }

    var owned = await ParseAndWaitAsync("1:a"u8.ToArray());
    Assert.True(owned.AtomBytes.SequenceEqual("a"u8));
  }
}
