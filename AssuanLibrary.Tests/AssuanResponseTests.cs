// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests;

[TestSubject(typeof(AssuanResponse))]
public sealed class AssuanResponseTests {
  [Test]
  public void Constructor_ShouldSetUnknownType_WhenBufferIsEmpty() {
    var response = new AssuanResponse(Array.Empty<byte>());

    response.Type.ShouldBe(AssuanResponseType.Unknown);
    response.Buffer.ShouldBeEmpty();
  }

  [Test]
  public void Constructor_ShouldParseType_AndBufferSeparatedBySpace() {
    var buffer = "OK value"u8.ToArray();

    var response = new AssuanResponse(buffer);

    response.Type.ShouldBe(AssuanResponseType.Ok);
    response.Buffer.ShouldBe("value"u8.ToArray());
  }

  [Test]
  public void Constructor_ShouldHandleTypeWithoutPayload() {
    var buffer = "ERR"u8.ToArray();

    var response = new AssuanResponse(buffer);

    response.Type.ShouldBe(AssuanResponseType.Error);
    response.Buffer.ShouldBeEmpty();
  }

  [Test]
  public void DecodedBuffer_ShouldReturnDecodedBytes() {
    var buffer = "D %41%42"u8.ToArray();

    var response = new AssuanResponse(buffer);

    response.DecodedBuffer.ShouldBe("AB"u8.ToArray());
  }

  [Test]
  public void Equals_ShouldReturnTrue_ForSameTypeAndBuffer() {
    var buffer = "OK test"u8.ToArray();

    var r1 = new AssuanResponse(buffer);
    var r2 = new AssuanResponse(buffer);

    r1.Equals(r2).ShouldBeTrue();
    r1.ShouldBe(r2);
  }

  [Test]
  public void Equals_ShouldReturnFalse_WhenTypesDiffer() {
    var r1 = new AssuanResponse("OK test"u8.ToArray());
    var r2 = new AssuanResponse("ERR test"u8.ToArray());

    r1.ShouldNotBe(r2);
  }

  [Test]
  public void Equals_ShouldReturnFalse_WhenBuffersDiffer() {
    var r1 = new AssuanResponse("OK a"u8.ToArray());
    var r2 = new AssuanResponse("OK b"u8.ToArray());

    r1.ShouldNotBe(r2);
  }

  [Test]
  public void Equals_ShouldReturnFalse_WhenOtherIsNull() {
    var response = new AssuanResponse("OK"u8.ToArray());

    response.Equals(null).ShouldBeFalse();
  }

  [Test]
  public void GetHashCode_ShouldBeEqual_ForEqualResponses() {
    var r1 = new AssuanResponse("OK test"u8.ToArray());
    var r2 = new AssuanResponse("OK test"u8.ToArray());

    r1.GetHashCode().ShouldBe(r2.GetHashCode());
  }

  [Test]
  public void GetHashCode_ShouldDiffer_WhenResponsesDiffer() {
    var r1 = new AssuanResponse("OK a"u8.ToArray());
    var r2 = new AssuanResponse("OK b"u8.ToArray());

    r1.GetHashCode().ShouldNotBe(r2.GetHashCode());
  }

  [Test]
  public void ToString_ShouldDefaultToDecodedString_ForNonDataResponses() {
    var response = new AssuanResponse("OK hello"u8.ToArray());

    response.ToString().ShouldBe("hello");
  }

  [Test]
  public void ToString_ShouldDefaultToHex_ForDataResponses() {
    var response = new AssuanResponse("D %41%42"u8.ToArray());

    response.ToString().ShouldBe("4142");
  }

  [Test]
  public void ToString_WithTSpecifier_ShouldIncludeType() {
    var response = new AssuanResponse("OK hello"u8.ToArray());

    response.ToString("TG", CultureInfo.InvariantCulture)
      .ShouldBe("OK hello");
  }

  [Test]
  public void ToString_WithHSpecifier_ShouldReturnHexOfDecodedBuffer() {
    var response = new AssuanResponse("OK %41"u8.ToArray());

    response.ToString("H", CultureInfo.InvariantCulture)
      .ShouldBe("41");
  }

  [Test]
  public void ToString_WithUnknownFormat_ShouldReturnEmptyString() {
    var response = new AssuanResponse("OK test"u8.ToArray());

    response.ToString("X", CultureInfo.InvariantCulture)
      .ShouldBe(string.Empty);
  }

  [Test]
  public void GetOriginalBuffer_ShouldReturnCopyOfOriginalBuffer() {
    var original = "OK test"u8.ToArray();
    var response = new AssuanResponse(original);

    var copy = response.GetOriginalBuffer();

    copy.ShouldBe(original);
    ReferenceEquals(copy, original).ShouldBeFalse();
  }
}
