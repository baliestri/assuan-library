// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;

namespace AssuanLibrary.Tests;

[TestSubject(typeof(AssuanResponseTypeExtensions))]
public sealed class AssuanResponseTypeExtensionsTests {
  [Test]
  public void Parse_ShouldReturnOk_ForOK() {
    var result = AssuanResponseType.Parse("OK"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Ok);
  }

  [Test]
  public void Parse_ShouldReturnError_ForERR() {
    var result = AssuanResponseType.Parse("ERR"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Error);
  }

  [Test]
  public void Parse_ShouldReturnStatus_ForS() {
    var result = AssuanResponseType.Parse("S"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Status);
  }

  [Test]
  public void Parse_ShouldReturnComment_ForHash() {
    var result = AssuanResponseType.Parse("#"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Comment);
  }

  [Test]
  public void Parse_ShouldReturnData_ForD() {
    var result = AssuanResponseType.Parse("D"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Data);
  }

  [Test]
  public void Parse_ShouldReturnInquire_ForINQUIRE() {
    var result = AssuanResponseType.Parse("INQUIRE"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Inquire);
  }

  [Test]
  public void Parse_ShouldIgnoreLineFeedSuffix() {
    var result = AssuanResponseType.Parse("OK\n"u8.ToArray());

    result.ShouldBe(AssuanResponseType.Ok);
  }

  [Test]
  public void Parse_ShouldThrow_WhenBufferIsEmpty()
    => Should.Throw<ArgumentOutOfRangeException>(() => AssuanResponseType.Parse([]));

  [Test]
  public void Parse_ShouldThrow_WhenBufferExceedsMaximumLength() {
    var buffer = "TOO-LONG"u8.ToArray();

    Should.Throw<ArgumentOutOfRangeException>(() => AssuanResponseType.Parse(buffer));
  }

  [Test]
  public void Parse_ShouldThrowNotSupported_ForUnknownPrefix() {
    var buffer = "XYZ"u8.ToArray();

    var ex = Should.Throw<NotSupportedException>(() => AssuanResponseType.Parse(buffer));

    ex.Message.ShouldContain("XYZ");
  }

  [Test]
  public void Parse_ShouldThrowNotSupported_ForPartialInquire() {
    var buffer = "INQ"u8.ToArray();

    Should.Throw<NotSupportedException>(() => AssuanResponseType.Parse(buffer));
  }
}
