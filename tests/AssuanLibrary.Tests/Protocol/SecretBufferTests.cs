// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using System.Reflection;

namespace AssuanLibrary.Tests.Protocol;

public sealed class SecretBufferTests {
  [Fact]
  public void AppendsWithoutGrowthAndDoesNotMutateOnCapacityFailure() {
    using var secret = new SecretBuffer(4);
    Assert.Equal(4, secret.Capacity);
    secret.Append("abc"u8);
    Assert.Equal(3, secret.Length);
    var error = Assert.Throws<LimitException>(() => secret.Append("de"u8));
    Assert.Equal(LimitErrorKind.CapacityExceeded, error.Kind);
    Assert.Equal(3, secret.Length);
    Assert.True(secret.Expose().SequenceEqual("abc"u8));
    Assert.DoesNotContain("abc", secret.ToString());
    Assert.DoesNotContain("abc", error.Message);
    secret.Append("d"u8);
    Assert.True(secret.Expose().SequenceEqual("abcd"u8));
  }

  [Fact]
  public void ClearAndDisposeWipeTheFullAllocationNotOnlyLogicalLength() {
    var secret = new SecretBuffer(16);
    var storage = Assert.IsType<byte[]>(typeof(SecretBuffer)
      .GetField("_storage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(secret));
    secret.Append("pass"u8);
    storage.AsSpan(4).Fill(0xaa);
    secret.Clear();
    Assert.True(storage.AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.Equal(16, secret.Capacity);
    Assert.Equal(0, secret.Length);
    secret.Append("secret"u8);
    secret.Dispose();
    secret.Dispose();
    Assert.True(storage.AsSpan().IndexOfAnyExcept((byte)0) < 0);
    Assert.Throws<ObjectDisposedException>(() => secret.Append("x"u8));
  }

  [Fact]
  public void ZeroCapacityIsValidAndNegativeIsRejectedBeforeAllocation() {
    using var empty = new SecretBuffer(0);
    Assert.Equal(0, empty.Capacity);
    Assert.True(empty.Expose().IsEmpty);
    Assert.Throws<LimitException>(() => empty.Append("x"u8));
    Assert.Throws<ArgumentOutOfRangeException>(() => new SecretBuffer(-1));
  }
}
