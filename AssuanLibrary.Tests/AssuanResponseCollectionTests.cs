// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;

namespace AssuanLibrary.Tests;

[TestSubject(typeof(AssuanResponseCollection))]
public sealed class AssuanResponseCollectionTests {
  [Test]
  public void Constructor_ShouldCreateEmptyCollection_WhenBufferIsEmpty() {
    var collection = new AssuanResponseCollection([]);

    collection.Count.ShouldBe(0);
    collection.ShouldBeEmpty();
  }

  [Test]
  public void Constructor_ShouldSplitResponses_OnLineFeed() {
    var buffer = "OK first\nERR second\nD %41%42\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.Count.ShouldBe(3);
    collection[0].Type.ShouldBe(AssuanResponseType.Ok);
    collection[1].Type.ShouldBe(AssuanResponseType.Error);
    collection[2].Type.ShouldBe(AssuanResponseType.Data);
  }

  [Test]
  public void Indexer_ShouldReturnResponseAtIndex() {
    var buffer = "OK a\nOK b\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection[1].ToString().ShouldBe("b");
  }

  [Test]
  public void Enumerator_ShouldEnumerateAllResponsesInOrder() {
    var buffer = "OK a\nERR b\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    var types = collection.Select(r => r.Type).ToArray();

    types.ShouldBe([
      AssuanResponseType.Ok,
      AssuanResponseType.Error
    ]);
  }

  [Test]
  public void ToString_ShouldDecodeEntireOriginalBuffer() {
    var buffer = "OK %41\nERR %42\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.ToString().ShouldBe("OK A\nERR B\n");
  }

  [Test]
  public void Constructor_ShouldHandleTrailingLineFeed() {
    var buffer = "OK a\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.Count.ShouldBe(1);
    collection[0].Type.ShouldBe(AssuanResponseType.Ok);
    collection[0].ToString().ShouldBe("a");
  }

  [Test]
  public void Constructor_ShouldCreateResponse_ForEmptyLine() {
    var buffer = "\n"u8.ToArray();

    var collection = new AssuanResponseCollection(buffer);

    collection.Count.ShouldBe(1);
    collection[0].Type.ShouldBe(AssuanResponseType.Unknown);
  }
}
