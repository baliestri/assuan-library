// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Protocol;

public sealed class LineBufferTests {
  [Fact]
  public void ReadsOnlyFirstLineAndRetainsTheSuffixForCaller() {
    using var frame = new ProtocolLineBuffer();
    var input = "OK\r\nNEXT\n"u8;
    Assert.Equal(4, frame.Feed(input));
    Assert.True(frame.IsReady);
    Assert.True(frame.Line.SequenceEqual("OK"u8));
    Assert.Equal(0, frame.Feed("ignored\n"u8));
    frame.FinishEof();
    frame.Clear();
    Assert.Equal(5, frame.Feed(input[4..]));
    Assert.True(frame.Line.SequenceEqual("NEXT"u8));
    frame.Clear();
    Assert.Equal(1, frame.Feed("\n"u8));
    Assert.True(frame.IsReady);
    Assert.True(frame.Line.IsEmpty);
  }

  [Fact]
  public void HandlesSplitCrLfAndReturnsMutableContentWithoutTerminator() {
    using var frame = new ProtocolLineBuffer();
    Assert.Equal(3, frame.Feed("D x"u8));
    Assert.Equal(1, frame.Feed("\r"u8));
    Assert.Equal(1, frame.Feed("\nrest"u8));
    Assert.True(frame.Line.SequenceEqual("D x"u8));
    frame.MutableLine[2] = (byte)'y';
    Assert.True(frame.Line.SequenceEqual("D y"u8));
    frame.Clear();
    Assert.Equal(4, frame.Feed("D x\r"u8));
    Assert.Equal(1, frame.Feed("\n"u8));
    Assert.True(frame.Line.SequenceEqual("D x"u8));
  }

  [Fact]
  public void BoundaryCountsLfAndCrLfAndFailureIsSticky() {
    foreach (var ending in new[] { "\n"u8.ToArray(), "\r\n"u8.ToArray() }) {
      foreach (var total in new[] { 999, 1000, 1001 }) {
        var input = new byte[total];
        Array.Fill(input, (byte)'x');
        ending.CopyTo(input.AsSpan(total - ending.Length));
        using var frame = new ProtocolLineBuffer();
        if (total <= ProtocolLineBuffer.MAX_LINE_BYTES) {
          Assert.Equal(total, frame.Feed(input));
          Assert.Equal(total - ending.Length, frame.Line.Length);
          frame.FinishEof();
        }
        else {
          Assert.Equal(ProtocolErrorKind.LineTooLong,
            Assert.Throws<ProtocolException>(() => frame.Feed(input)).Kind);
          Assert.False(frame.IsReady);
          Assert.Equal(ProtocolErrorKind.LineTooLong,
            Assert.Throws<ProtocolException>(() => frame.Feed("\n"u8)).Kind);
          Assert.Equal(ProtocolErrorKind.LineTooLong,
            Assert.Throws<ProtocolException>(frame.FinishEof).Kind);
          frame.Clear();
          Assert.Equal(3, frame.Feed("OK\n"u8));
        }
      }
    }
  }

  [Fact]
  public void EofDistinguishesPartialReadyAndEmptyState() {
    using var frame = new ProtocolLineBuffer();
    frame.FinishEof();
    Assert.False(frame.IsReady);
    Assert.Equal(3, frame.Feed("OK\r"u8));
    Assert.Equal(ProtocolErrorKind.UnexpectedEof,
      Assert.Throws<ProtocolException>(frame.FinishEof).Kind);
    Assert.Equal(1, frame.Feed("\nignored"u8));
    frame.FinishEof();
  }

  [Fact]
  public void ClearFailureAndDisposeWipeEntireCapacity() {
    var frame = new ProtocolLineBuffer();
    var storage = Assert.IsType<byte[]>(typeof(ProtocolLineBuffer)
      .GetField("_storage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(frame));
    Assert.Equal(1, frame.Feed("s"u8));
    storage.AsSpan(1).Fill(0xaa);
    frame.Clear();
    Assert.True(storage.AsSpan().IndexOfAnyExcept((byte)0) < 0);

    var overlong = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
    Array.Fill(overlong, (byte)'x');
    Assert.Throws<ProtocolException>(() => frame.Feed(overlong));
    Assert.True(storage.AsSpan().IndexOfAnyExcept((byte)0) < 0);
    frame.Clear();
    frame.Feed("D secret\n"u8);
    Assert.DoesNotContain("secret", frame.ToString());
    frame.Dispose();
    frame.Dispose();
    Assert.True(storage.AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.Throws<ObjectDisposedException>(() => frame.Feed("OK\n"u8));
  }

  [Fact]
  public void EveryPartitionPreservesBothFrames() {
    var input = "D a%0Ab\r\nOK\n"u8;
    for (var mask = 0; mask < (1 << (input.Length - 1)); ++mask) {
      using var frame = new ProtocolLineBuffer();
      var lines = new List<byte[]>();
      var start = 0;
      for (var end = 1; end <= input.Length; ++end) {
        if (end != input.Length &&
            (mask & (1 << (end - 1))) == 0) {
          continue;
        }

        var chunk = input[start..end];
        while (!chunk.IsEmpty) {
          var consumed = frame.Feed(chunk);
          Assert.True(consumed > 0);
          chunk = chunk[consumed..];
          if (frame.IsReady) {
            lines.Add(frame.Line.ToArray());
            frame.Clear();
          }
        }

        start = end;
      }

      Assert.Equal(2, lines.Count);
      Assert.True(lines[0].AsSpan().SequenceEqual("D a%0Ab"u8));
      Assert.True(lines[1].AsSpan().SequenceEqual("OK"u8));
      frame.FinishEof();
    }
  }
}
