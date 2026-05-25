// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Protocol;

[TestSubject(typeof(AssuanResponseCollection))]
public sealed class AssuanResponseCollectionTests {
  [Fact]
  public void Constructor_ShouldCreateEmptyCollection_WhenBufferIsEmpty() {
    var collection = new AssuanResponseCollection([]);

    collection.Count.ShouldBe(0);
    collection.ShouldBeEmpty();
  }

  [Fact]
  public void Constructor_ShouldSplitResponses_OnLineFeed() {
    var buffer = "OK first\nERR second\nD %41%42\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.Count.ShouldBe(3);
    collection[0].Type.ShouldBe(AssuanResponseType.Ok);
    collection[1].Type.ShouldBe(AssuanResponseType.Error);
    collection[2].Type.ShouldBe(AssuanResponseType.Data);
  }

  [Fact]
  public void Indexer_ShouldReturnResponseAtIndex() {
    var buffer = "OK a\nOK b\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection[1].ToString().ShouldBe("b");
  }

  [Fact]
  public void Enumerator_ShouldEnumerateAllResponsesInOrder() {
    var buffer = "OK a\nERR b\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    var types = collection.Select(r => r.Type).ToArray();

    types.ShouldBe([
      AssuanResponseType.Ok,
      AssuanResponseType.Error
    ]);
  }

  [Fact]
  public void ToString_ShouldDecodeEntireOriginalBuffer() {
    var buffer = "OK %41\nERR %42\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.ToString().ShouldBe("OK A\nERR B\n");
  }

  [Fact]
  public void Constructor_ShouldHandleTrailingLineFeed() {
    var buffer = "OK a\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.Count.ShouldBe(1);
    collection[0].Type.ShouldBe(AssuanResponseType.Ok);
    collection[0].ToString().ShouldBe("a");
  }

  [Fact]
  public void Constructor_ShouldCreateResponse_ForEmptyLine() {
    var buffer = "\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.Count.ShouldBe(1);
    collection[0].Type.ShouldBe(AssuanResponseType.Unknown);
  }

  [Fact]
  public void Constructor_ShouldKeepIncompleteLine_AsUnknownResponse() {
    var collection = new AssuanResponseCollection("OK ready\nINQ"u8.ToArray());

    collection.Count.ShouldBe(2);
    collection[0].Type.ShouldBe(AssuanResponseType.Ok);
    collection[1].Type.ShouldBe(AssuanResponseType.Unknown);
  }

  [Fact]
  public void Constructor_ShouldParseMultipleDataResponses() {
    var collection = new AssuanResponseCollection("D one\nD two\nOK\n"u8.ToArray());

    collection.Count.ShouldBe(3);
    collection[0].Type.ShouldBe(AssuanResponseType.Data);
    collection[1].Type.ShouldBe(AssuanResponseType.Data);
    collection[2].Type.ShouldBe(AssuanResponseType.Ok);
  }

  [Fact]
  public void Constructor_ShouldParseInquireDataEndSequence() {
    var collection = new AssuanResponseCollection("INQUIRE KEY\nD value\nEND\n"u8.ToArray());

    collection.Count.ShouldBe(3);
    collection[0].Type.ShouldBe(AssuanResponseType.Inquire);
    collection[1].Type.ShouldBe(AssuanResponseType.Data);
    collection[2].Type.ShouldBe(AssuanResponseType.End);
  }
}
