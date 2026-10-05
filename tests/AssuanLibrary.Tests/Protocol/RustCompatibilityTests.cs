// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Protocol;

public sealed class RustCompatibilityTests {
  [Fact]
  public void BinaryPayloadRoundTripsAcrossAsManyDLinesAsNeeded() {
    var input = new byte[8192];
    for (var index = 0; index < input.Length; ++index) {
      input[index] = (byte)index;
    }

    var remaining = input.AsSpan();
    var collected = new List<byte>();
    var output = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
    var chunks = 0;
    while (!remaining.IsEmpty) {
      var (consumed, written) = ProtocolEncoder.EncodeDataChunk(remaining, output);
      Assert.True(consumed > 0);
      Assert.InRange(written, 3, ProtocolLineBuffer.MAX_LINE_BYTES);
      using var frame = new ProtocolLineBuffer();
      Assert.Equal(written, frame.Feed(output.AsSpan(0, written)));
      var message = ProtocolResponse.Parse(frame.Line);
      Assert.Equal(ProtocolResponseKind.Data, message.Kind);
      var decoded = ProtocolEscaping.DecodeDataInPlace(frame.MutableLine[2..]);
      collected.AddRange(frame.Line.Slice(2, decoded).ToArray());
      remaining = remaining[consumed..];
      ++chunks;
    }

    Assert.True(chunks > 1);
    Assert.Equal(input, collected);
  }

  [Fact]
  public void AFramedCrNotFollowedByLfIsRejectedByTheParser() {
    using var frame = new ProtocolLineBuffer();
    Assert.Equal(5, frame.Feed("OK\rX\n"u8));
    Assert.Equal(ProtocolErrorKind.InvalidLine, WireParsingTests.ResponseError(frame.Line).Kind);
  }

  [Fact]
  public void ThePublicSensitivitySelectorDoesNotExposeSecretBytes() {
    Assert.NotEqual(PayloadSensitivity.Public, PayloadSensitivity.Secret);
    using var secret = new SecretBuffer(4);
    secret.Append(new byte[] { 0, 0xff, (byte)'\r', (byte)'\n' });
    Assert.True(secret.Expose().SequenceEqual(new byte[] { 0, 0xff, (byte)'\r', (byte)'\n' }));
    Assert.DoesNotContain("255", secret.ToString());
  }
}
