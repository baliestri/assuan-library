// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Protocol;

public sealed class EncodingTests {
  [Fact]
  public void ValidatesEscapesBeforeMutatingInput() {
    foreach (var invalid in new[] { "%", "%0", "%GG", "%0x", "%41%" }) {
      var bytes = Encoding.ASCII.GetBytes(invalid);
      Assert.Equal(ProtocolErrorKind.InvalidEscape,
        Assert.Throws<ProtocolException>(() => ProtocolEscaping.DecodeDataInPlace(bytes)).Kind);
      Assert.Equal(invalid, Encoding.ASCII.GetString(bytes));
    }

    for (var value = 0; value <= 255; ++value) {
      foreach (var encoded in new[] { $"%{value:X2}", $"%{value:x2}" }) {
        var bytes = Encoding.ASCII.GetBytes(encoded);
        Assert.Equal(1, ProtocolEscaping.DecodeDataInPlace(bytes));
        Assert.Equal((byte)value, bytes[0]);
      }
    }

    var prefix = "%2541"u8.ToArray();
    var length = ProtocolEscaping.DecodeDataInPlace(prefix);
    Assert.True(prefix.AsSpan(0, length).SequenceEqual("%41"u8));
  }

  [Fact]
  public void CommandEncodingIsAtomicAndPreservesWireArguments() {
    var output = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
    Array.Fill(output, (byte)0xaa);
    var length = ProtocolEncoder.EncodeCommand(ProtocolCommand.Parse("NOP"u8), output);
    Assert.True(output.AsSpan(0, length).SequenceEqual("NOP\n"u8));
    Assert.True(output.AsSpan(length).ToArray().All(b => b == 0xaa));

    var arguments = new byte[997];
    Array.Fill(arguments, (byte)'x');
    Assert.Equal(1000, ProtocolEncoder.EncodeCommand(ProtocolCommand.Create("X"u8, arguments), output));
    var before = output.ToArray();
    Assert.Equal(ProtocolErrorKind.LineTooLong,
      Assert.Throws<ProtocolException>(() => ProtocolEncoder.EncodeCommand(ProtocolCommand.Create("XX"u8, arguments), output)).Kind);
    Assert.Equal(before, output);
    length = ProtocolEncoder.EncodeCommand(ProtocolCommand.Create("CUSTOM"u8, " %GG "u8), output);
    Assert.True(output.AsSpan(0, length).SequenceEqual("CUSTOM  %GG \n"u8));
  }

  [Fact]
  public void RawBinaryDataSplitsWithoutSplittingEscapes() {
    var output = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
    Assert.Equal((0, 3), ProtocolEncoder.EncodeDataChunk(ReadOnlySpan<byte>.Empty, output));
    Assert.True(output.AsSpan(0, 3).SequenceEqual("D \n"u8));
    var source = new byte[] { (byte)'%', (byte)'\r', (byte)'\n', 0, 0xff, (byte)' ' };
    var (consumed, written) = ProtocolEncoder.EncodeDataChunk(source, output);
    Assert.Equal(source.Length, consumed);
    ReadOnlySpan<byte> expected = [
      (byte)'D', (byte)' ', (byte)'%', (byte)'2', (byte)'5', (byte)'%', (byte)'0', (byte)'D',
      (byte)'%', (byte)'0', (byte)'A', (byte)'%', (byte)'0', (byte)'0', 0xff, (byte)' ', (byte)'\n'
    ];
    Assert.True(output.AsSpan(0, written).SequenceEqual(expected));
    foreach (var plain in new[] { 994, 995, 996, 997 }) {
      var bytes = new byte[plain + 1];
      Array.Fill(bytes, (byte)'a');
      bytes[^1] = (byte)'%';
      (consumed, written) = ProtocolEncoder.EncodeDataChunk(bytes, output);
      Assert.Equal(plain == 994 ? 995 : plain, consumed);
      Assert.InRange(written, 1, 1000);
      var decoded = ProtocolEscaping.DecodeDataInPlace(output.AsSpan(2, written - 3));
      Assert.True(output.AsSpan(2, decoded).SequenceEqual(bytes.AsSpan(0, consumed)));
    }
  }

  [Fact]
  public void ResponseEncodingValidatesEntireLineBeforeWriting() {
    var output = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
    Array.Fill(output, (byte)0xaa);
    AssertWire(ProtocolResponse.Ok("done"u8), "OK done\n"u8, output);
    AssertWire(ProtocolResponse.Err(uint.MaxValue, " failure"u8), "ERR 4294967295  failure\n"u8, output);
    AssertWire(ProtocolResponse.Data(" %ff"u8), "D  %ff\n"u8, output);
    AssertWire(ProtocolResponse.Status("_STATUS"u8, " x"u8), "S _STATUS  x\n"u8, output);
    AssertWire(ProtocolResponse.Inquire("foo"u8, ReadOnlySpan<byte>.Empty), "INQUIRE foo\n"u8, output);
    AssertWire(ProtocolResponse.End(), "END\n"u8, output);
    AssertWire(ProtocolResponse.Comment(" note"u8), "# note\n"u8, output);
    AssertWire(ProtocolResponse.Empty(), "\n"u8, output);
    var before = output.ToArray();
    Assert.Equal(ProtocolErrorKind.InvalidEscape,
      Assert.Throws<ProtocolException>(() => ProtocolEncoder.EncodeResponse(ProtocolResponse.Data("%GG"u8), output)).Kind);
    Assert.Equal(before, output);
    Assert.Equal(ProtocolErrorKind.InvalidLine,
      Assert.Throws<ProtocolException>(() => ProtocolEncoder.EncodeResponse(ProtocolResponse.Ok("x\n"u8), output)).Kind);
    Assert.Equal(before, output);
  }

  private static void AssertWire(ProtocolResponse response, ReadOnlySpan<byte> expected, Span<byte> output) {
    var length = ProtocolEncoder.EncodeResponse(response, output);
    Assert.True(output[..length].SequenceEqual(expected));
  }
}
