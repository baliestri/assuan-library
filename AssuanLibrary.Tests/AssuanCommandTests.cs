// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;

namespace AssuanLibrary.Tests;

[TestSubject(typeof(AssuanCommand))]
public sealed class AssuanCommandTests {
  [Test]
  public void Constructor_ShouldTrimCommandName_AndInitializeCount() {
    var command = new AssuanCommand("  TEST  ");

    command.Name.ShouldBe("TEST");
    command.Count.ShouldBe(1);
  }

  [Test]
  public void Constructor_ShouldThrow_WhenCommandNameIsNullOrWhitespace() {
    Should.Throw<ArgumentException>(() => new AssuanCommand(""));
    Should.Throw<ArgumentException>(() => new AssuanCommand("   "));
    Should.Throw<ArgumentException>(() => new AssuanCommand(null!));
  }

  [Test]
  public void Indexer_Get_ShouldReturnEntry() {
    var command = new AssuanCommand("CMD");
    command.Add("arg");

    command[1].ShouldBe("arg");
  }

  [Test]
  public void Indexer_Get_ShouldThrow_WhenOutOfRange() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => _ = command[-1]);
    Should.Throw<ArgumentOutOfRangeException>(() => _ = command[1]);
  }

  [Test]
  public void Indexer_Set_ShouldTrimValue() {
    var command = new AssuanCommand("CMD");
    command.Add("arg");

    command[1] = "  value  ";

    command[1].ShouldBe("value");
  }

  [Test]
  public void Indexer_Set_ShouldRemoveEntry_WhenValueIsWhitespace() {
    var command = new AssuanCommand("CMD");
    command.Add("arg1");
    command.Add("arg2");

    command[1] = "   ";

    command.Count.ShouldBe(2);
    command[1].ShouldBe("arg2");
  }

  [Test]
  public void Add_ShouldAppendArgument() {
    var command = new AssuanCommand("CMD");

    command.Add("arg");

    command.Count.ShouldBe(2);
    command[1].ShouldBe("arg");
  }

  [Test]
  public void Add_ShouldQuoteArgument_WhenContainingSpaces() {
    var command = new AssuanCommand("CMD");

    command.Add("hello world");

    command[1].ShouldBe("¨hello world¨");
  }

  [Test]
  public void Add_ShouldThrow_WhenArgumentIsNullOrWhitespace() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentException>(() => command.Add(""));
    Should.Throw<ArgumentException>(() => command.Add("   "));
    Should.Throw<ArgumentException>(() => command.Add(null!));
  }

  [Test]
  public void Remove_ShouldRemoveArgumentByValue() {
    var command = new AssuanCommand("CMD");
    command.Add("a");
    command.Add("b");

    command.Remove("a");

    command.Count.ShouldBe(2);
    command[1].ShouldBe("b");
  }

  [Test]
  public void Remove_ShouldThrow_WhenArgumentNotFound() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => command.Remove("missing"));
  }

  [Test]
  public void RemoveAt_ShouldThrow_WhenIndexIsZero() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => command.RemoveAt(0));
  }

  [Test]
  public void RemoveAt_ShouldThrow_WhenIndexIsOutOfRange() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => command.RemoveAt(1));
  }

  [Test]
  public void Enumerator_ShouldReturnNameAndArgumentsInOrder() {
    var command = new AssuanCommand("CMD");
    command.Add("a");
    command.Add("b");

    command.ToArray().ShouldBe(new[] { "CMD", "a", "b" });
  }

  [Test]
  public void Equals_ShouldReturnTrue_ForSameEntries() {
    var left = new AssuanCommand("CMD");
    left.Add("a");

    var right = new AssuanCommand("CMD");
    right.Add("a");

    left.ShouldBe(right);
    (left == right).ShouldBeTrue();
  }

  [Test]
  public void Equals_ShouldReturnFalse_WhenEntriesDiffer() {
    var left = new AssuanCommand("CMD");
    left.Add("a");

    var right = new AssuanCommand("CMD");
    right.Add("b");

    left.ShouldNotBe(right);
    (left != right).ShouldBeTrue();
  }

  [Test]
  public void Equals_ShouldReturnFalse_WhenOtherIsNull() {
    var command = new AssuanCommand("CMD");

    command.Equals(null).ShouldBeFalse();
  }

  [Test]
  public void GetHashCode_ShouldBeDeterministic() {
    var command = new AssuanCommand("CMD");
    command.Add("a");

    var hash1 = command.GetHashCode();
    var hash2 = command.GetHashCode();

    hash1.ShouldBe(hash2);
  }

  [Test]
  public void GetHashCode_ShouldChange_WhenOrderChanges() {
    var c1 = new AssuanCommand("CMD");
    c1.Add("a");
    c1.Add("b");

    var c2 = new AssuanCommand("CMD");
    c2.Add("b");
    c2.Add("a");

    c1.GetHashCode().ShouldNotBe(c2.GetHashCode());
  }

  [Test]
  public void GetHashCode_ShouldMatchRollingHashAlgorithm() {
    var command = new AssuanCommand("CMD");
    command.Add("a");
    command.Add("b");

    var expected = 17;
    expected = (expected * 31) + "CMD".GetHashCode();
    expected = (expected * 31) + "a".GetHashCode();
    expected = (expected * 31) + "b".GetHashCode();

    command.GetHashCode().ShouldBe(expected);
  }

  [Test]
  public void ToString_ShouldReturnNonEmptyValue() {
    var command = new AssuanCommand("CMD");
    command.Add("a");

    command.ToString().ShouldNotBeNullOrWhiteSpace();
  }

  [Test]
  public void ToBytes_ShouldReturnNonEmptyArray() {
    var command = new AssuanCommand("CMD");
    command.Add("a");

    command.ToBytes().Length.ShouldBeGreaterThan(0);
  }

  [Test]
  public void ToReadOnlyMemory_ShouldReturnNonEmptyMemory() {
    var command = new AssuanCommand("CMD");
    command.Add("a");

    command.ToReadOnlyMemory().Length.ShouldBeGreaterThan(0);
  }
}
