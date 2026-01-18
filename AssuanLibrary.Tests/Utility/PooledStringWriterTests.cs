// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Utility;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Utility;

[TestSubject(typeof(PooledStringWriter))]
public sealed class PooledStringWriterTests {
  [Test]
  public void Should_Write_CorrectlySingleChar() {
    using var writer = new PooledStringWriter(16);

    writer.Write('x');
    writer.Write('Y');
    writer.Write("🚀");

    writer.ToString().ShouldBe("xY🚀");
  }

  [Test]
  public void Should_Write_CorrectlyString() {
    using var writer = new PooledStringWriter(32);

    writer.Write("Hello");
    writer.Write(" ");
    writer.Write("世界");

    writer.ToString().ShouldBe("Hello 世界");
  }

  [Test]
  public void Should_Write_NullOrEmptyStringDoesNothing() {
    using var writer = new PooledStringWriter(16);

    writer.Write(null);
    writer.Write(string.Empty);

    writer.ToString().ShouldBeEmpty();
  }

  [Test]
  public void Should_Write_CorrectlyReadOnlySpan() {
    using var writer = new PooledStringWriter(64);

    writer.Write("Start".AsSpan());
    writer.Write(" → ".AsSpan());
    writer.Write("End".AsSpan());

    writer.ToString().ShouldBe("Start → End");
  }

  [Test]
  public void Should_Grow_BufferWhenNeeded() {
    using var writer = new PooledStringWriter(4);

    const string LONG_TEXT = "This is quite a long text that should definitely force buffer growth multiple times";

    writer.Write(LONG_TEXT);

    var result = writer.ToString();

    result.ShouldBe(LONG_TEXT);
    result.Length.ShouldBe(LONG_TEXT.Length);
  }

  [Test]
  public void Should_Intermix_WritesCorrectly() {
    using var writer = new PooledStringWriter(8);

    writer.Write('A');
    writer.Write("BC");
    writer.Write("DEFG".AsSpan());
    writer.Write(" → ");
    writer.Write("🔥");

    writer.ToString().ShouldBe("ABCDEFG → 🔥");
  }

  [Test]
  public void Should_GetSpan_AllowDirectWriting() {
    using var writer = new PooledStringWriter(32);

    var span = writer.GetSpan(10);
    "Hello".AsSpan().CopyTo(span);
    writer.Advance(5);

    span = writer.GetSpan(6);
    "World!".AsSpan().CopyTo(span);
    writer.Advance(6);

    writer.ToString().ShouldBe("HelloWorld!");
  }

  [Test]
  public void Should_ToString_DisposeAfterwards() {
    var writer = new PooledStringWriter(16);
    writer.Write("Important");

    var result = writer.ToString();

    result.ShouldBe("Important");

    Should.Throw<ObjectDisposedException>(() => writer.Write('x'));
  }

  [Test]
  public void Should_ToString_WithMaxLength_TruncateCorrectlyAndDispose() {
    using var writer = new PooledStringWriter(64);

    writer.Write("This is a very long message that we want to truncate");

    var truncated = writer.ToString(10);

    truncated.ShouldBe("This is a ");
    truncated.Length.ShouldBe(10);

    Should.Throw<ObjectDisposedException>(() => writer.Write('x'));
  }

  [Test]
  public void Should_Dispose_BeIdempotent() {
    var writer = new PooledStringWriter(32);
    writer.Write("test");

    writer.Dispose();
    writer.Dispose();

    Should.Throw<ObjectDisposedException>(() => writer.Write('x'));
  }

  [Test]
  public void Should_Throw_ObjectDisposedException_AfterDispose() {
    var writer = new PooledStringWriter(16);
    writer.Dispose();

    Should.Throw<ObjectDisposedException>(() => writer.Write('a'));
    Should.Throw<ObjectDisposedException>(() => writer.Write("hello"));
    Should.Throw<ObjectDisposedException>(() => writer.Write("hi".AsSpan()));
    Should.Throw<ObjectDisposedException>(() => writer.Advance(3));
    Should.Throw<ObjectDisposedException>(() => writer.GetSpan(10));
    Should.Throw<ObjectDisposedException>(() => writer.GetMemory(20));
    Should.Throw<ObjectDisposedException>(() => writer.ToString());
  }

  [Test]
  public void Should_Throw_ArgumentOutOfRangeException_ForNegativeSizes() {
    using var writer = new PooledStringWriter(16);

    Should.Throw<ArgumentOutOfRangeException>(() => writer.Advance(-1));
    Should.Throw<ArgumentOutOfRangeException>(() => writer.GetSpan(-5));
    Should.Throw<ArgumentOutOfRangeException>(() => writer.GetMemory(-100));
  }
}
