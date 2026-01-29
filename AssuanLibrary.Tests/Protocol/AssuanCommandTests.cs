// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Protocol;

[TestSubject(typeof(AssuanCommand))]
public sealed class AssuanCommandTests {
  [Fact]
  public void Constructor_ShouldTrimCommandName_AndInitializeCount() {
    var command = new AssuanCommand("  TEST  ");

    command.Name.ShouldBe("TEST");
    command.Count.ShouldBe(1);
  }

  [Fact]
  public void Constructor_ShouldThrow_WhenCommandNameIsNullOrWhitespace() {
    Should.Throw<ArgumentException>(() => new AssuanCommand(""));
    Should.Throw<ArgumentException>(() => new AssuanCommand("   "));
    Should.Throw<ArgumentException>(() => new AssuanCommand((string)null!));
  }

  [Fact]
  public void Indexer_Get_ShouldReturnEntry() {
    var command = new AssuanCommand("CMD") { "arg" };

    command[1].ShouldBe("arg");
  }

  [Fact]
  public void Indexer_Get_ShouldThrow_WhenOutOfRange() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => _ = command[-1]);
    Should.Throw<ArgumentOutOfRangeException>(() => _ = command[1]);
  }

  [Fact]
  public void Indexer_Set_ShouldTrimValue() {
    var command = new AssuanCommand("CMD") { "arg" };

    command[1] = "  value  ";

    command[1].ShouldBe("value");
  }

  [Fact]
  public void Indexer_Set_ShouldRemoveEntry_WhenValueIsWhitespace() {
    var command = new AssuanCommand("CMD") {
      "arg1",
      "arg2"
    };

    command[1] = "   ";

    command.Count.ShouldBe(2);
    command[1].ShouldBe("arg2");
  }

  [Fact]
  public void Add_ShouldAppendArgument() {
    var command = new AssuanCommand("CMD") { "arg" };

    command.Count.ShouldBe(2);
    command[1].ShouldBe("arg");
  }

  [Fact]
  public void Add_ShouldThrow_WhenArgumentIsNullOrWhitespace() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentException>(() => command.Add(""));
    Should.Throw<ArgumentException>(() => command.Add("   "));
    Should.Throw<ArgumentException>(() => command.Add(null!));
  }

  [Fact]
  public void Remove_ShouldRemoveArgumentByValue() {
    var command = new AssuanCommand("CMD") {
      "a",
      "b"
    };

    command.Remove("a");

    command.Count.ShouldBe(2);
    command[1].ShouldBe("b");
  }

  [Fact]
  public void Remove_ShouldThrow_WhenArgumentNotFound() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => command.Remove("missing"));
  }

  [Fact]
  public void RemoveAt_ShouldThrow_WhenIndexIsZero() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => command.RemoveAt(0));
  }

  [Fact]
  public void RemoveAt_ShouldThrow_WhenIndexIsOutOfRange() {
    var command = new AssuanCommand("CMD");

    Should.Throw<ArgumentOutOfRangeException>(() => command.RemoveAt(1));
  }

  [Fact]
  public void Enumerator_ShouldReturnNameAndArgumentsInOrder() {
    var command = new AssuanCommand("CMD") {
      "a",
      "b"
    };

    command.ToArray().ShouldBe(["CMD", "a", "b"]);
  }

  [Fact]
  public void Equals_ShouldReturnTrue_ForSameEntries() {
    var left = new AssuanCommand("CMD") { "a" };

    var right = new AssuanCommand("CMD") { "a" };

    left.ShouldBe(right);
    (left == right).ShouldBeTrue();
  }

  [Fact]
  public void Equals_ShouldReturnFalse_WhenEntriesDiffer() {
    var left = new AssuanCommand("CMD") { "a" };

    var right = new AssuanCommand("CMD") { "b" };

    left.ShouldNotBe(right);
    (left != right).ShouldBeTrue();
  }

  [Fact]
  public void Equals_ShouldReturnFalse_WhenOtherIsNull() {
    var command = new AssuanCommand("CMD");

    command.Equals(null).ShouldBeFalse();
  }

  [Fact]
  public void GetHashCode_ShouldBeDeterministic() {
    var command = new AssuanCommand("CMD") { "a" };

    var hash1 = command.GetHashCode();
    var hash2 = command.GetHashCode();

    hash1.ShouldBe(hash2);
  }

  [Fact]
  public void GetHashCode_ShouldChange_WhenOrderChanges() {
    var c1 = new AssuanCommand("CMD") {
      "a",
      "b"
    };

    var c2 = new AssuanCommand("CMD") {
      "b",
      "a"
    };

    c1.GetHashCode().ShouldNotBe(c2.GetHashCode());
  }

  [Fact]
  public void GetHashCode_ShouldMatchRollingHashAlgorithm() {
    var command = new AssuanCommand("CMD") {
      "a",
      "b"
    };

    var expected = 17;
    expected = (expected * 31) + "CMD".GetHashCode();
    expected = (expected * 31) + "a".GetHashCode();
    expected = (expected * 31) + "b".GetHashCode();

    command.GetHashCode().ShouldBe(expected);
  }

  [Fact]
  public void ToString_ShouldReturnNonEmptyValue() {
    var command = new AssuanCommand("CMD") { "a" };

    command.ToString().ShouldNotBeNullOrWhiteSpace();
  }

  [Fact]
  public void ToBytes_ShouldReturnNonEmptyArray() {
    var command = new AssuanCommand("CMD") { "a" };

    command.ToBytes().Length.ShouldBeGreaterThan(0);
  }

  [Fact]
  public void ToReadOnlyMemory_ShouldReturnNonEmptyMemory() {
    var command = new AssuanCommand("CMD") { "a" };

    command.ToReadOnlyMemory().Length.ShouldBeGreaterThan(0);
  }
}
