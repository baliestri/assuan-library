// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Protocol;

[TestSubject(typeof(AssuanResponseTypeExtensions))]
public sealed class AssuanResponseTypeExtensionsTests {
  [Fact]
  public void Parse_ShouldReturnOk_ForOK() {
    var result = AssuanResponseType.Parse("OK"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Ok);
  }

  [Fact]
  public void Parse_ShouldReturnError_ForERR() {
    var result = AssuanResponseType.Parse("ERR"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Error);
  }

  [Fact]
  public void Parse_ShouldReturnStatus_ForS() {
    var result = AssuanResponseType.Parse("S"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Status);
  }

  [Fact]
  public void Parse_ShouldReturnComment_ForHash() {
    var result = AssuanResponseType.Parse("#"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Comment);
  }

  [Fact]
  public void Parse_ShouldReturnData_ForD() {
    var result = AssuanResponseType.Parse("D"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Data);
  }

  [Fact]
  public void Parse_ShouldReturnInquire_ForINQUIRE() {
    var result = AssuanResponseType.Parse("INQUIRE"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Inquire);
  }

  [Fact]
  public void Parse_ShouldIgnoreLineFeedSuffix() {
    var result = AssuanResponseType.Parse("OK\n"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Ok);
  }

  [Fact]
  public void Parse_ShouldThrow_WhenBufferIsEmpty()
    => Should.Throw<ArgumentOutOfRangeException>(() => AssuanResponseType.Parse([]));

  [Fact]
  public void Parse_ShouldThrow_WhenBufferExceedsMaximumLength() {
    var buffer = "TOO-LONG"u8.ToArray();

    Should.Throw<ArgumentOutOfRangeException>(() => AssuanResponseType.Parse(buffer));
  }

  [Fact]
  public void Parse_ShouldThrowNotSupported_ForUnknownPrefix() {
    var buffer = "XYZ"u8.ToArray();

    var ex = Should.Throw<NotSupportedException>(() => AssuanResponseType.Parse(buffer));

    ex.Message.ShouldContain("XYZ");
  }

  [Fact]
  public void Parse_ShouldThrowNotSupported_ForPartialInquire() {
    var buffer = "INQ"u8.ToArray();

    Should.Throw<NotSupportedException>(() => AssuanResponseType.Parse(buffer));
  }
}
