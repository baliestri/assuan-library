// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests;

[TestSubject(typeof(AssuanEncoder))]
public sealed class AssuanEncoderTests {
  [Test]
  public void AsString_ShouldReturnEmpty_WhenValueIsNullOrWhitespace() {
    AssuanEncoder.AsString(string.Empty).ShouldBeEmpty();
    AssuanEncoder.AsString("   ").ShouldBeEmpty();
    AssuanEncoder.AsString(null!).ShouldBeEmpty();
  }

  [Test]
  public void AsBytes_ShouldReturnEmpty_WhenValueIsNullOrWhitespace() {
    AssuanEncoder.AsBytes(string.Empty).ShouldBeEmpty();
    AssuanEncoder.AsBytes("   ").ShouldBeEmpty();
    AssuanEncoder.AsBytes(null!).ShouldBeEmpty();
  }

  [Test]
  public void AsReadOnlyMemory_ShouldReturnEmpty_WhenValueIsNullOrWhitespace() {
    AssuanEncoder.AsReadOnlyMemory(string.Empty).IsEmpty.ShouldBeTrue();
    AssuanEncoder.AsReadOnlyMemory("   ").IsEmpty.ShouldBeTrue();
    AssuanEncoder.AsReadOnlyMemory((string)null!).IsEmpty.ShouldBeTrue();
  }

  [Test]
  public void SafeCharacters_ShouldPassThroughUnchanged() {
    const string INPUT = "ABCxyz123-_.~";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe(INPUT);
  }

  [Test]
  public void UnsafeCharacters_ShouldBePercentEncoded() {
    const string INPUT = "%";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("%25");
  }

  [Test]
  public void NonAsciiCharacters_ShouldBePercentEncoded() {
    const string INPUT = "é"; // 0xE9

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("%E9");
  }

  [Test]
  public void Spaces_ShouldBeEncoded_WhenEscapeModeIsEnabled() {
    const string INPUT = "hello¨ world¨";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("hello%20world");
  }

  [Test]
  public void Spaces_ShouldRemainUnencoded_WhenEscapeModeIsDisabled() {
    const string INPUT = "hello world";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("hello world");
  }

  [Test]
  public void EscapeDelimiter_ShouldToggleEscapeMode() {
    const string INPUT = "a¨ b ¨c";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("a%20b%20c");
  }

  [Test]
  public void AsString_ShouldAppendLineFeed_ByDefault() {
    var result = AssuanEncoder.AsString("CMD");

    result.EndsWith("\n").ShouldBeTrue();
  }

  [Test]
  public void AsString_ShouldNotAppendLineFeed_WhenDisabled() {
    var result = AssuanEncoder.AsString("CMD", false);

    result.EndsWith("\n").ShouldBeFalse();
  }

  [Test]
  public void AsBytes_ShouldAppendLineFeed_ByDefault() {
    var bytes = AssuanEncoder.AsBytes("CMD");

    bytes.Last().ShouldBe((byte)'\n');
  }

  [Test]
  public void AsString_And_AsBytes_ShouldProduceEquivalentOutput() {
    const string INPUT = "hello¨ world¨";

    var str = AssuanEncoder.AsString(INPUT);
    var bytes = AssuanEncoder.AsBytes(INPUT);

    Encoding.ASCII.GetBytes(str).ShouldBe(bytes);
  }

  [Test]
  public void AsBytes_And_AsReadOnlyMemory_ShouldProduceEquivalentOutput() {
    const string INPUT = "CMD arg";

    var bytes = AssuanEncoder.AsBytes(INPUT);
    var memory = AssuanEncoder.AsReadOnlyMemory(INPUT);

    memory.ToArray().ShouldBe(bytes);
  }

  [Test]
  public void AsReadOnlyMemory_FromByteArray_ShouldReturnEmpty_WhenInputIsEmpty() {
    var result = AssuanEncoder.AsReadOnlyMemory([]);

    result.IsEmpty.ShouldBeTrue();
  }
}
