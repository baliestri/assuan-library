// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using AssuanLibrary.Buffers;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Buffers;

[TestSubject(typeof(PooledByteWriter))]
public sealed class PooledByteWriterTests {
  [Test]
  public void Should_Write_CorrectlySingleByte() {
    using var writer = new PooledByteWriter(16);

    writer.Write(42);

    var result = writer.ToArray();
    result.ShouldBeEquivalentTo(new byte[] { 42 });
  }

  [Test]
  public void Should_Advance_IncreaseWrittenCount() {
    using var writer = new PooledByteWriter(32);

    writer.Advance(5);
    writer.Advance(3);

    writer.ToArray().Length.ShouldBe(8);
  }

  [Test]
  public void Should_GetSpan_AllowDirectWriting() {
    using var writer = new PooledByteWriter(64);

    var span = writer.GetSpan(8);
    span[0] = 10;
    span[1] = 20;
    span[2] = 30;

    writer.Advance(3);

    writer.ToArray().ShouldBeEquivalentTo(new byte[] { 10, 20, 30 });
  }

  [Test]
  public void Should_Grow_BufferWhenNeeded() {
    using var writer = new PooledByteWriter(4);

    for (var i = 0; i < 20; i++) {
      writer.Write((byte)(i + 1));
    }

    var result = writer.ToArray();

    result.Length.ShouldBe(20);
    result.ShouldBeEquivalentTo(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 });
  }

  [Test]
  public void Should_SizeHint_BeRespectedWhenPossible() {
    using var writer = new PooledByteWriter(8);

    var memory = writer.GetMemory(32);
    memory.Length.ShouldBeGreaterThanOrEqualTo(32);
  }

  [Test]
  public void Should_SizeHint_ZeroGiveAtLeastOneByte() {
    using var writer = new PooledByteWriter(4);

    var span = writer.GetSpan();
    span.Length.ShouldBeGreaterThan(0);
  }

  [Test]
  public void Should_GetSpan_MultipleCallsWorkCorrectly() {
    using var writer = new PooledByteWriter(16);

    writer.GetSpan(4).Fill(0xAA);
    writer.Advance(4);

    writer.GetSpan(8).Fill(0xBB);
    writer.Advance(8);

    writer.ToArray().ShouldBeEquivalentTo(
      new byte[] { 0xAA, 0xAA, 0xAA, 0xAA, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB });
  }

  [Test]
  public void Should_ToReadOnlyMemory_ReturnsCorrectData() {
    using var writer = new PooledByteWriter(32);

    writer.Write(0xDE);
    writer.Write(0xAD);
    writer.Write(0xBE);
    writer.Write(0xEF);

    var rom = writer.ToReadOnlyMemory();

    rom.Length.ShouldBe(4);
    rom.Span.ToArray().ShouldBeEquivalentTo(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
  }

  [Test]
  public void Should_NotReturn_SameBufferAfterDispose() {
    byte[] originalBuffer;

    using (var writer = new PooledByteWriter(128)) {
      originalBuffer = writer.GetSpan().ToArray();
      writer.Write(1);
    }

    var newBuffer = ArrayPool<byte>.Shared.Rent(128);

    newBuffer.ShouldNotBeSameAs(originalBuffer);
  }

  [Test]
  public void Should_Throw_ObjectDisposedException_AfterDispose() {
    var writer = new PooledByteWriter(16);
    writer.Dispose();

    Should.Throw<ObjectDisposedException>(() => writer.Write(1));
    Should.Throw<ObjectDisposedException>(() => writer.Advance(5));
    Should.Throw<ObjectDisposedException>(() => writer.GetSpan(10));
    Should.Throw<ObjectDisposedException>(() => writer.ToArray());
  }

  [Test]
  public void Should_Throw_ArgumentOutOfRangeException_ForNegativeSizes() {
    var writer = new PooledByteWriter(16);

    Should.Throw<ArgumentOutOfRangeException>(() => writer.Advance(-1));
    Should.Throw<ArgumentOutOfRangeException>(() => writer.GetSpan(-5));
    Should.Throw<ArgumentOutOfRangeException>(() => writer.GetMemory(-15));
  }
}
