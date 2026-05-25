// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.Protocol;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Protocol;

[TestSubject(typeof(AssuanResponse))]
public sealed class AssuanResponseTests {
  [Fact]
  public void Constructor_ShouldSetUnknownType_WhenBufferIsEmpty() {
    var response = new AssuanResponse(Array.Empty<byte>());

    response.Type.ShouldBe(AssuanResponseType.Unknown);
    response.Buffer.ShouldBeEmpty();
  }

  [Fact]
  public void Constructor_ShouldParseType_AndBufferSeparatedBySpace() {
    var buffer = "OK value"u8.ToArray();

    var response = new AssuanResponse(buffer);

    response.Type.ShouldBe(AssuanResponseType.Ok);
    response.Buffer.ShouldBe("value"u8.ToArray());
  }

  [Fact]
  public void Constructor_ShouldHandleTypeWithoutPayload() {
    var buffer = "ERR"u8.ToArray();

    var response = new AssuanResponse(buffer);

    response.Type.ShouldBe(AssuanResponseType.Error);
    response.Buffer.ShouldBeEmpty();
  }

  [Fact]
  public void Constructor_ShouldParseInquireResponses() {
    var response = new AssuanResponse("INQUIRE KEYWORD"u8.ToArray());

    response.Type.ShouldBe(AssuanResponseType.Inquire);
    response.Buffer.ShouldBe("KEYWORD"u8.ToArray());
  }

  [Fact]
  public void Constructor_ShouldFallbackToUnknown_ForUnknownResponseType() {
    var response = new AssuanResponse("WHAT payload"u8.ToArray());

    response.Type.ShouldBe(AssuanResponseType.Unknown);
    response.Buffer.ShouldBe("WHAT payload"u8.ToArray());
  }

  [Fact]
  public void Constructor_ShouldFallbackToUnknown_ForIncompleteResponseType() {
    var response = new AssuanResponse("INQ"u8.ToArray());

    response.Type.ShouldBe(AssuanResponseType.Unknown);
    response.Buffer.ShouldBe("INQ"u8.ToArray());
  }

  [Fact]
  public void Constructor_ShouldPreserveInvalidUtf8PayloadBytes() {
    var response = new AssuanResponse([0x44, 0x20, 0xFF, 0xFE]);

    response.Type.ShouldBe(AssuanResponseType.Data);
    response.Buffer.ShouldBe([0xFF, 0xFE]);
  }

  [Fact]
  public void DecodedBuffer_ShouldReturnDecodedBytes() {
    var buffer = "D %41%42"u8.ToArray();

    var response = new AssuanResponse(buffer);

    response.DecodedBuffer.ShouldBe("AB"u8.ToArray());
  }

  [Fact]
  public void Equals_ShouldReturnTrue_ForSameTypeAndBuffer() {
    var buffer = "OK test"u8.ToArray();

    var r1 = new AssuanResponse(buffer);
    var r2 = new AssuanResponse(buffer);

    r1.Equals(r2).ShouldBeTrue();
    r1.ShouldBe(r2);
  }

  [Fact]
  public void Equals_ShouldReturnFalse_WhenTypesDiffer() {
    var r1 = new AssuanResponse("OK test"u8.ToArray());
    var r2 = new AssuanResponse("ERR test"u8.ToArray());

    r1.ShouldNotBe(r2);
  }

  [Fact]
  public void Equals_ShouldReturnFalse_WhenBuffersDiffer() {
    var r1 = new AssuanResponse("OK a"u8.ToArray());
    var r2 = new AssuanResponse("OK b"u8.ToArray());

    r1.ShouldNotBe(r2);
  }

  [Fact]
  public void Equals_ShouldReturnFalse_WhenOtherIsNull() {
    var response = new AssuanResponse("OK"u8.ToArray());

    response.Equals(null).ShouldBeFalse();
  }

  [Fact]
  public void GetHashCode_ShouldBeEqual_ForEqualResponses() {
    var r1 = new AssuanResponse("OK test"u8.ToArray());
    var r2 = new AssuanResponse("OK test"u8.ToArray());

    r1.GetHashCode().ShouldBe(r2.GetHashCode());
  }

  [Fact]
  public void GetHashCode_ShouldDiffer_WhenResponsesDiffer() {
    var r1 = new AssuanResponse("OK a"u8.ToArray());
    var r2 = new AssuanResponse("OK b"u8.ToArray());

    r1.GetHashCode().ShouldNotBe(r2.GetHashCode());
  }

  [Fact]
  public void ToString_ShouldDefaultToDecodedString_ForNonDataResponses() {
    var response = new AssuanResponse("OK hello"u8.ToArray());

    response.ToString().ShouldBe("hello");
  }

  [Fact]
  public void ToString_ShouldDefaultToHex_ForDataResponses() {
    var response = new AssuanResponse("D %41%42"u8.ToArray());

    response.ToString().ShouldBe("4142");
  }

  [Fact]
  public void ToString_WithTSpecifier_ShouldIncludeType() {
    var response = new AssuanResponse("OK hello"u8.ToArray());

    response.ToString("TG", CultureInfo.InvariantCulture)
      .ShouldBe("OK hello");
  }

  [Fact]
  public void ToString_WithHSpecifier_ShouldReturnHexOfDecodedBuffer() {
    var response = new AssuanResponse("OK %41"u8.ToArray());

    response.ToString("H", CultureInfo.InvariantCulture)
      .ShouldBe("41");
  }

  [Fact]
  public void ToString_WithUnknownFormat_ShouldReturnEmptyString() {
    var response = new AssuanResponse("OK test"u8.ToArray());

    response.ToString("X", CultureInfo.InvariantCulture)
      .ShouldBe(string.Empty);
  }

  [Fact]
  public void GetOriginalBuffer_ShouldReturnCopyOfOriginalBuffer() {
    var original = "OK test"u8.ToArray();
    var response = new AssuanResponse(original);

    var copy = response.GetOriginalBuffer();

    copy.ShouldBe(original);
    ReferenceEquals(copy, original).ShouldBeFalse();
  }

  [Fact]
  public void ToString_WithTSpecifierOnly_ShouldReturnTypeValue() {
    var response = new AssuanResponse("OK hello"u8.ToArray());

    response.ToString("T", CultureInfo.InvariantCulture).ShouldBe("OK");
  }

  [Fact]
  public void ToString_WithTD_OnDataResponse_ShouldReturnHex() {
    var response = new AssuanResponse("D %41%42"u8.ToArray());

    response.ToString("D", CultureInfo.InvariantCulture).ShouldBe("4142");
  }

  [Fact]
  public void ToString_WithFormatLengthGreaterThan2_ShouldFallbackToDefault() {
    var response = new AssuanResponse("OK hello"u8.ToArray());

    response.ToString("TGX", CultureInfo.InvariantCulture).ShouldBe("hello");
  }

  [Fact]
  public void Ok_ShouldCreateOkResponse_WithLineFeedAtEnd() {
    var response = AssuanResponse.Ok();

    response.Type.ShouldBe(AssuanResponseType.Ok);
    response.Buffer.ShouldBe([0x0A]);
  }

  [Fact]
  public void Error_ShouldCreateErrorResponse_WithLineFeedAtEnd() {
    var response = AssuanResponse.Error(1, "message");

    response.Type.ShouldBe(AssuanResponseType.Error);
    response.Buffer.ShouldBe("1 message\n"u8.ToArray());
  }

  [Fact]
  public void Status_ShouldCreateStatusResponse_WithLineFeedAtEnd() {
    var response = AssuanResponse.Status("status message");

    response.Type.ShouldBe(AssuanResponseType.Status);
    response.Buffer.ShouldBe("status message\n"u8.ToArray());
  }

  [Fact]
  public void Comment_ShouldCreateCommentResponse_WithLineFeedAtEnd() {
    var response = AssuanResponse.Comment();

    response.Type.ShouldBe(AssuanResponseType.Comment);
    response.Buffer.ShouldBe([0x0A]);
  }

  [Fact]
  public void Data_ShouldCreateDataResponse_WithEncodedBufferAndLineFeed() {
    var response = AssuanResponse.Data("AB CD");

    response.Type.ShouldBe(AssuanResponseType.Data);
    response.Buffer.ShouldBe("AB CD\n"u8.ToArray());
  }

  [Fact]
  public void Inquire_ShouldCreateInquireResponse_WithEncodedBufferAndLineFeed() {
    var response = AssuanResponse.Inquire("Inquiry data", "--param");

    response.Type.ShouldBe(AssuanResponseType.Inquire);
    response.Buffer.ShouldBe("Inquiry data --param\n"u8.ToArray());
  }
}
