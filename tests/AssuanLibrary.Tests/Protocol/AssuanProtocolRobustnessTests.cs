// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Protocol;

public sealed class AssuanProtocolRobustnessTests {
  [Fact]
  public void EncoderDecoder_ShouldRoundTripLargePayload() {
    var payload = new string('A', 200_000);

    var encoded = AssuanEncoder.AsString(payload, false, true);
    var decoded = AssuanDecoder.ToString(encoded);

    decoded.ShouldBe(payload);
  }

  [Fact]
  public void ResponseCollection_ShouldPreserveUnknownLinesWithoutThrowing() {
    var collection = new AssuanResponseCollection("OK good\n??? weird\nERR 1 failed\n"u8.ToArray());

    collection.Count.ShouldBe(3);
    collection[0].Type.ShouldBe(AssuanResponseType.Ok);
    collection[1].Type.ShouldBe(AssuanResponseType.Unknown);
    collection[2].Type.ShouldBe(AssuanResponseType.Error);
  }
}

