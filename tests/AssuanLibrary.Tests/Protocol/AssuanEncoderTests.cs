// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Protocol;

[TestSubject(typeof(AssuanEncoder))]
public sealed class AssuanEncoderTests {
  [Fact]
  public void AsString_ShouldReturnEmpty_WhenValueIsNullOrWhitespace() {
    AssuanEncoder.AsString(string.Empty).ShouldBeEmpty();
    AssuanEncoder.AsString("   ").ShouldBeEmpty();
    AssuanEncoder.AsString(null!).ShouldBeEmpty();
  }

  [Fact]
  public void AsBytes_ShouldReturnEmpty_WhenValueIsNullOrWhitespace() {
    AssuanEncoder.AsBytes(string.Empty).ShouldBeEmpty();
    AssuanEncoder.AsBytes("   ").ShouldBeEmpty();
    AssuanEncoder.AsBytes((string)null!).ShouldBeEmpty();
  }

  [Fact]
  public void AsReadOnlyMemory_ShouldReturnEmpty_WhenValueIsNullOrWhitespace() {
    AssuanEncoder.AsReadOnlyMemory(string.Empty).IsEmpty.ShouldBeTrue();
    AssuanEncoder.AsReadOnlyMemory("   ").IsEmpty.ShouldBeTrue();
    AssuanEncoder.AsReadOnlyMemory((string)null!).IsEmpty.ShouldBeTrue();
  }

  [Fact]
  public void SafeCharacters_ShouldPassThroughUnchanged() {
    const string INPUT = "ABCxyz123-_.~";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe(INPUT);
  }

  [Fact]
  public void UnsafeCharacters_ShouldBePercentEncoded() {
    const string INPUT = "%";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("%25");
  }

  [Fact]
  public void AsString_ShouldEncodeCarriageReturnAndLineFeed() {
    var result = AssuanEncoder.AsString("a\rb\nc", false);

    result.ShouldBe("a%0Db%0Ac");
  }

  [Fact]
  public void NonAsciiCharacters_ShouldBePercentEncoded() {
    const string INPUT = "é"; // 0xE9

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("%E9");
  }

  [Fact]
  public void Spaces_ShouldRemainUnencoded_WhenEscapeModeIsDisabled() {
    const string INPUT = "hello world";

    var result = AssuanEncoder.AsString(INPUT, false);

    result.ShouldBe("hello world");
  }

  [Fact]
  public void AsString_ShouldAppendLineFeed_ByDefault() {
    var result = AssuanEncoder.AsString("CMD");

    result.EndsWith("\n").ShouldBeTrue();
  }

  [Fact]
  public void AsString_ShouldNotAppendLineFeed_WhenDisabled() {
    var result = AssuanEncoder.AsString("CMD", false);

    result.EndsWith("\n").ShouldBeFalse();
  }

  [Fact]
  public void AsBytes_ShouldAppendLineFeed_ByDefault() {
    var bytes = AssuanEncoder.AsBytes("CMD");

    bytes.Last().ShouldBe((byte)'\n');
  }

  [Fact]
  public void AsString_And_AsBytes_ShouldProduceEquivalentOutput() {
    const string INPUT = "hello¨ world¨";

    var str = AssuanEncoder.AsString(INPUT);
    var bytes = AssuanEncoder.AsBytes(INPUT);

    Encoding.ASCII.GetBytes(str).ShouldBe(bytes);
  }

  [Fact]
  public void AsBytes_And_AsReadOnlyMemory_ShouldProduceEquivalentOutput() {
    const string INPUT = "CMD arg";

    var bytes = AssuanEncoder.AsBytes(INPUT);
    var memory = AssuanEncoder.AsReadOnlyMemory(INPUT);

    memory.ToArray().ShouldBe(bytes);
  }

  [Fact]
  public void AsReadOnlyMemory_FromByteArray_ShouldReturnEmpty_WhenInputIsEmpty() {
    var result = AssuanEncoder.AsReadOnlyMemory([]);

    result.IsEmpty.ShouldBeTrue();
  }

  [Fact]
  public void AsString_ShouldEncodeSpaces_WhenEncodeSpaceIsTrue() {
    AssuanEncoder.AsString("a b", false, true)
      .ShouldBe("a%20b");
  }

  [Fact]
  public void AsString_ShouldNotDoubleEncode_ExistingPercentTriplets() {
    AssuanEncoder.AsString("%41", false).ShouldBe("%41");
    AssuanEncoder.AsString("%41", false, true).ShouldBe("%41");
  }

  [Fact]
  public void AsString_ShouldEncodeSinglePercentCharacter() {
    AssuanEncoder.AsString("value%", false).ShouldBe("value%25");
  }

  [Fact]
  public void IsEncoded_ShouldReturnTrue_ForEmptyValues() {
    AssuanEncoder.IsEncoded(string.Empty).ShouldBeTrue();
    AssuanEncoder.IsEncoded(ReadOnlySpan<byte>.Empty).ShouldBeTrue();
  }

  [Theory]
  [InlineData("%41")]
  [InlineData("a%20b")]
  [InlineData("%6a")]
  public void IsEncoded_ShouldReturnTrue_ForValidPercentTriplets(string input) => AssuanEncoder.IsEncoded(input).ShouldBeTrue();

  [Theory]
  [InlineData("%")]
  [InlineData("ABC%")]
  [InlineData("%4")]
  [InlineData("%G1")]
  [InlineData("%1G")]
  public void IsEncoded_ShouldReturnFalse_ForInvalidPercentTriplets(string input) => AssuanEncoder.IsEncoded(input).ShouldBeFalse();

  [Fact]
  public void IsEncoded_ShouldReturnFalse_ForNonAsciiCharacters() => AssuanEncoder.IsEncoded("olé").ShouldBeFalse();

  [Fact]
  public void IsEncoded_ShouldRespectEncodeSpaceFlag() {
    AssuanEncoder.IsEncoded("a b", true).ShouldBeFalse();
    AssuanEncoder.IsEncoded("a%20b", true).ShouldBeTrue();
  }

  [Fact]
  public void IsEncoded_BytesOverload_ShouldReturnFalse_ForNonAsciiBytes() {
    ReadOnlySpan<byte> value = [0x80];
    AssuanEncoder.IsEncoded(value).ShouldBeFalse();
  }
}
