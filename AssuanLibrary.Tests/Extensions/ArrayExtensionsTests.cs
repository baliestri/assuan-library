// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Extensions;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Extensions;

[TestSubject(typeof(ArrayExtensions))]
public sealed class ArrayExtensionsTests {
  [Test]
  public void Skip_ShouldReturnEmptyArray_WhenSourceIsEmpty() {
    var source = Array.Empty<int>();

    var result = source.Skip(1);

    result.ShouldBeEmpty();
  }

  [Test]
  public void Skip_ShouldReturnElementsAfterDelimiter_WhenDelimiterIsFound() {
    var source = new[] { 1, 2, 3, 4, 5 };

    var result = source.Skip(3);

    result.ShouldBe([4, 5]);
  }

  [Test]
  public void Skip_ShouldReturnEmptyArray_WhenDelimiterIsLastElement() {
    var source = new[] { 1, 2, 3 };

    var result = source.Skip(3);

    result.ShouldBeEmpty();
  }

  [Test]
  public void Skip_ShouldReturnEmptyArray_WhenDelimiterIsNotFound() {
    var source = new[] { 1, 2, 3 };

    var result = source.Skip(99);

    result.ShouldBeEmpty();
  }

  [Test]
  public void Skip_ShouldUseFirstOccurrenceOfDelimiter() {
    var source = new[] { 1, 2, 2, 3 };

    var result = source.Skip(2);

    result.ShouldBe([2, 3]);
  }

  [Test]
  public void Take_ShouldReturnSameArray_WhenSourceIsEmpty() {
    var source = Array.Empty<int>();

    var result = source.Take(1);

    result.ShouldBeSameAs(source);
  }

  [Test]
  public void Take_ShouldReturnElementsBeforeDelimiter_WhenDelimiterIsFound() {
    var source = new[] { 1, 2, 3, 4 };

    var result = source.Take(3);

    result.ShouldBe([1, 2]);
  }

  [Test]
  public void Take_ShouldReturnEmptyArray_WhenDelimiterIsFirstElement() {
    var source = new[] { 1, 2, 3 };

    var result = source.Take(1);

    result.ShouldBeEmpty();
  }

  [Test]
  public void Take_ShouldReturnOriginalArray_WhenDelimiterIsNotFound() {
    var source = new[] { 1, 2, 3 };

    var result = source.Take(99);

    result.ShouldBeSameAs(source);
  }

  [Test]
  public void Take_ShouldUseFirstOccurrenceOfDelimiter() {
    var source = new[] { 1, 2, 2, 3 };

    var result = source.Take(2);

    result.ShouldBe([1]);
  }

  [Test]
  public void Split_ShouldYieldNoResults_WhenSourceIsEmpty() {
    var source = Array.Empty<int>();

    var result = source.Split(1).ToArray();

    result.ShouldBeEmpty();
  }

  [Test]
  public void Split_ShouldSplitOnDelimiter_WithoutIncludingDelimiter() {
    var source = new[] { 1, 2, 0, 3, 4, 0, 5 };

    var result = source.Split(0).ToArray();

    result.Length.ShouldBe(3);
    result[0].ShouldBe([1, 2]);
    result[1].ShouldBe([3, 4]);
    result[2].ShouldBe([5]);
  }

  [Test]
  public void Split_ShouldSplitOnDelimiter_IncludingDelimiter() {
    var source = new[] { 1, 2, 0, 3, 0, 4 };

    var result = source.Split(0, true).ToArray();

    result.Length.ShouldBe(3);
    result[0].ShouldBe([1, 2, 0]);
    result[1].ShouldBe([3, 0]);
    result[2].ShouldBe([4]);
  }

  [Test]
  public void Split_ShouldHandleConsecutiveDelimiters() {
    var source = new[] { 1, 0, 0, 2 };

    var result = source.Split(0).ToArray();

    result.Length.ShouldBe(3);
    result[0].ShouldBe([1]);
    result[1].ShouldBeEmpty();
    result[2].ShouldBe([2]);
  }

  [Test]
  public void Split_ShouldReturnSingleSegment_WhenDelimiterIsNotFound() {
    var source = new[] { 1, 2, 3 };

    var result = source.Split(0).ToArray();

    result.Length.ShouldBe(1);
    result[0].ShouldBe([1, 2, 3]);
  }

  [Test]
  public void GetSequenceHashCode_ShouldBeDeterministic() {
    var source = new[] { 1, 2, 3 };

    var hash1 = source.GetSequenceHashCode();
    var hash2 = source.GetSequenceHashCode();

    hash1.ShouldBe(hash2);
  }

  [Test]
  public void GetSequenceHashCode_ShouldChangeWhenSequenceChanges() {
    var source1 = new[] { 1, 2, 3 };
    var source2 = new[] { 3, 2, 1 };

    var hash1 = source1.GetSequenceHashCode();
    var hash2 = source2.GetSequenceHashCode();

    hash1.ShouldNotBe(hash2);
  }

  [Test]
  public void GetSequenceHashCode_ShouldMatchXorOfElementHashCodes() {
    var source = new[] { 10, 20, 30 };

    var expected = 17;
    expected = (expected * 31) + 10.GetHashCode();
    expected = (expected * 31) + 20.GetHashCode();
    expected = (expected * 31) + 30.GetHashCode();

    source.GetSequenceHashCode().ShouldBe(expected);
  }
}
