// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Protocol;

public sealed class AssuanResponseParserTests {
  [Fact]
  public void Parse_ShouldCreateResponseCollection_FromByteArray() {
    var parser = new AssuanResponseParser();

    var responses = parser.Parse("OK hello\n"u8.ToArray());

    responses.Count.ShouldBe(1);
    responses[0].Type.ShouldBe(AssuanResponseType.Ok);
    responses[0].ToString().ShouldBe("hello");
  }

  [Fact]
  public void Parse_ShouldCreateResponseCollection_FromMemory() {
    var parser = new AssuanResponseParser();

    var responses = parser.Parse("D data\nOK\n"u8.ToArray().AsMemory());

    responses.Count.ShouldBe(2);
    responses[0].Type.ShouldBe(AssuanResponseType.Data);
    responses[1].Type.ShouldBe(AssuanResponseType.Ok);
  }
}

