// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Tests.Transport;

public sealed class WindowsAgentFileTests {
  [Fact]
  public async Task PendingReadOwnsAndWipesItsBufferAfterTimeout() {
    await using var stream = new DelayedReadStream();
    var read = WindowsSocketFile.ReadStreamAsync(stream,
      new TransportDeadline(TimeSpan.FromMilliseconds(100)),
      TestContext.Current.CancellationToken);
    await stream._started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    var error = await Assert.ThrowsAsync<DiscoveryException>(() => read);
    Assert.Equal(DiscoveryErrorKind.Timeout, error.Kind);
    stream.FinishAfterTimeout();
    Assert.True(SpinWait.SpinUntil(() => stream.PendingBuffer.Span.IndexOfAnyExcept((byte)0) < 0,
      TimeSpan.FromSeconds(2)), "Temporary bytes were not wiped after the delayed OS-style read.");
  }

  [Fact]
  public void ParsesBinaryNonceWithoutTreatingItAsText() {
    byte[] file = [
      .. "32123\n"u8.ToArray(),
      0, 10, 13, 255, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15
    ];
    var (port, nonce) = WindowsSocketFile.Parse(file);
    using (nonce) {
      Assert.Equal((ushort)32123, port);
      Assert.True(nonce.Expose().SequenceEqual(file.AsSpan(6)));
      Assert.DoesNotContain("32123", nonce.ToString());
    }

    var maximum = WindowsSocketFile.Parse([.. "65535\n"u8.ToArray(), .. new byte[16]]);
    using (maximum.Nonce) {
      Assert.Equal((ushort)65535, maximum.Port);
    }
  }

  [Fact]
  public void RejectsNonNativeFormatsAndExcessBytes() {
    byte[][] cases = [
      "!<socket >32123 s xxxxxxxxxxxxxxxxx"u8.ToArray(),
      "0\nxxxxxxxxxxxxxxxx"u8.ToArray(),
      "65536\nxxxxxxxxxxxxxxxx"u8.ToArray(),
      "1\r\nxxxxxxxxxxxxxxxx"u8.ToArray(),
      "-1\nxxxxxxxxxxxxxxxx"u8.ToArray(),
      "1\nshort"u8.ToArray(),
      [.. "1\n"u8.ToArray(), .. new byte[17]],
      new byte[1024]
    ];

    for (var index = 0; index < cases.Length; ++index) {
      var error = Assert.Throws<DiscoveryException>(() => WindowsSocketFile.Parse(cases[index]));
      Assert.Equal(index == 0 ? DiscoveryErrorKind.UnsupportedFormat : DiscoveryErrorKind.InvalidOutput, error.Kind);
      Assert.DoesNotContain("xxxxxxxx", error.ToString());
    }
  }

  private sealed class DelayedReadStream : MemoryStream {
    private readonly TaskCompletionSource<int> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Memory<byte> PendingBuffer { get; private set; }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
      PendingBuffer = buffer;
      _started.SetResult();
      return new ValueTask<int>(_release.Task);
    }

    internal void FinishAfterTimeout() {
      PendingBuffer.Span[0] = 0xff;
      _release.SetResult(1);
    }
  }
}
