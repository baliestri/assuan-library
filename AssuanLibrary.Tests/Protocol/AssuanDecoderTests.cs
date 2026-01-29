// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Protocol;

[TestSubject(typeof(AssuanDecoder))]
public sealed class AssuanDecoderTests {
  [Fact]
  public void ToBytes_ShouldReturnEmpty_WhenStringIsNullOrWhitespace() {
    AssuanDecoder.ToBytes(string.Empty).ShouldBeEmpty();
    AssuanDecoder.ToBytes("   ").ShouldBeEmpty();
    AssuanDecoder.ToBytes((string)null!).ShouldBeEmpty();
  }

  [Fact]
  public void ToString_ShouldReturnEmpty_WhenInputIsEmpty() {
    AssuanDecoder.ToString(string.Empty).ShouldBeEmpty();
    AssuanDecoder.ToString([]).ShouldBeEmpty();
    AssuanDecoder.ToString(ReadOnlyMemory<byte>.Empty).ShouldBeEmpty();
  }

  [Fact]
  public void ValidPercentEncodedSequence_ShouldBeDecoded() {
    var result = AssuanDecoder.ToString("%41%42%43");

    result.ShouldBe("ABC");
  }

  [Fact]
  public void LowercaseHex_ShouldBeDecoded() {
    var result = AssuanDecoder.ToString("%6a");

    result.ShouldBe("j");
  }

  [Fact]
  public void InvalidPercentSequence_ShouldBeLeftUntouched() {
    var result = AssuanDecoder.ToString("%ZZ");

    result.ShouldBe("%ZZ");
  }

  [Fact]
  public void TrailingPercent_ShouldBePreserved() {
    var result = AssuanDecoder.ToString("ABC%");

    result.ShouldBe("ABC%");
  }

  [Fact]
  public void ToBytes_FromString_And_FromSpan_ShouldMatch() {
    const string INPUT = "%41%42";

    var fromString = AssuanDecoder.ToBytes(INPUT);
    var fromSpan = AssuanDecoder.ToBytes(Encoding.ASCII.GetBytes(INPUT));

    fromString.ShouldBe(fromSpan);
  }

  [Fact]
  public void ToReadOnlyMemory_ShouldMatch_ToBytes() {
    const string INPUT = "%41%42";

    var bytes = AssuanDecoder.ToBytes(INPUT);
    var memory = AssuanDecoder.ToReadOnlyMemory(INPUT);

    memory.ToArray().ShouldBe(bytes);
  }

  [Fact]
  public void ToString_FromBytes_And_FromMemory_ShouldMatch() {
    var input = "%41%42"u8.ToArray();

    AssuanDecoder.ToString(input)
      .ShouldBe(AssuanDecoder.ToString(input.AsMemory()));
  }

  [Fact]
  public void EncoderAndDecoder_ShouldRoundTrip_String() {
    const string ORIGINAL = "hello world";

    var encoded = AssuanEncoder.AsString(ORIGINAL, false);
    var decoded = AssuanDecoder.ToString(encoded);

    decoded.ShouldBe(ORIGINAL);
  }

  [Fact]
  public void ToBytes_ShouldDecodePercentEncodedBytes() {
    var input = "%41%42"u8.ToArray();

    var result = AssuanDecoder.ToBytes(input);

    result.ShouldBe([0x41, 0x42]);
  }

  [Fact]
  public void ToBytes_ShouldPreserveInvalidPercentSequences() {
    var input = "%G1"u8.ToArray();

    var result = AssuanDecoder.ToBytes(input);

    result.ShouldBe(input);
  }

  [Fact]
  public void GetInquireParameters_ShouldReturnEmpty_WhenBufferIsEmpty() {
    var result = AssuanDecoder.GetInquireParameters(ReadOnlySpan<byte>.Empty);

    result.ShouldBeEmpty();
  }

  [Fact]
  public void GetInquireParameters_ShouldSplitOnSpaces() {
    var input = "a b c"u8.ToArray();

    var result = AssuanDecoder.GetInquireParameters(input);

    result.ShouldBe(["a", "b", "c"]);
  }

  [Fact]
  public void GetInquireParameters_ShouldDecodePercentEncodedParameters() {
    var input = "hello%20world test"u8.ToArray();

    var result = AssuanDecoder.GetInquireParameters(input);

    result.ShouldBe(["hello world", "test"]);
  }

  [Fact]
  public void GetInquireParameters_ShouldIgnoreExtraWhitespace() {
    var input = "  a   b\t c  "u8.ToArray();

    var result = AssuanDecoder.GetInquireParameters(input);

    result.ShouldBe(["a", "b", "c"]);
  }
}
