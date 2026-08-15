// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using AssuanLibrary.Platform.Unix.Transport.IO;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Unix.Transport.IO;

[Collection(nameof(PlatformTransportCollection))]
public sealed class StabilizedSocketReaderTests {
  [Fact]
  public void Read_ShouldReturnData_WhenDataIsImmediatelyAvailable() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var reader = new StabilizedSocketReader(pair.Server, 500, LoopbackHarness.FastStabilization());

    pair.Client.Send("hello"u8);

    var result = reader.Read();

    result.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public async Task ReadAsync_ShouldReturnData_WhenDataIsImmediatelyAvailable() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var reader = new StabilizedSocketReader(pair.Server, 500, LoopbackHarness.FastStabilization());

    await pair.Client.SendAsync("hello"u8.ToArray());

    var result = await reader.ReadAsync();

    result.ToArray().ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public void Read_ShouldThrowTimeoutException_WhenNoDataArrivesBeforeTimeout() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var reader = new StabilizedSocketReader(pair.Server, 100, LoopbackHarness.FastStabilization());

    Should.Throw<TimeoutException>(() => reader.Read());
  }

  [Fact]
  public async Task ReadAsync_ShouldThrowTimeoutException_WhenNoDataArrivesBeforeTimeout() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var reader = new StabilizedSocketReader(pair.Server, 100, LoopbackHarness.FastStabilization());

    await Should.ThrowAsync<TimeoutException>(async () => await reader.ReadAsync());
  }

  [Fact]
  public async Task Read_ShouldWaitForStabilization_WhenDataArrivesInTrickledChunks() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var options = LoopbackHarness.FastStabilization();
    using var reader = new StabilizedSocketReader(pair.Server, 2000, options);

    var writerTask = Task.Run(() => {
      pair.Client.Send("one-"u8);
      Thread.Sleep((int)(options.Delay.TotalMilliseconds / 2));
      pair.Client.Send("two"u8);
    });

    var result = reader.Read();
    await writerTask;

    result.ShouldBe("one-two"u8.ToArray());
  }

  [Fact]
  public void Read_ShouldNotStopEarly_WhenPeerClosesAfterPartialData() {
    // Unlike StabilizedTcpClientReader, this reader has no Connected check in the loop,
    // so it only stops via the idle/stabilization path, not an early disconnect break.
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var options = LoopbackHarness.FastStabilization();
    using var reader = new StabilizedSocketReader(pair.Server, 2000, options);

    pair.Client.Send("partial"u8);
    pair.Client.Shutdown(System.Net.Sockets.SocketShutdown.Both);
    pair.Client.Close();

    var sw = Stopwatch.StartNew();
    var result = reader.Read();
    sw.Stop();

    result.ShouldBe("partial"u8.ToArray());
    sw.Elapsed.ShouldBeGreaterThanOrEqualTo(options.Delay - TimeSpan.FromMilliseconds(15));
  }

  [Fact]
  public void Dispose_ShouldBeIdempotent_WhenCalledMultipleTimes() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var reader = new StabilizedSocketReader(pair.Server, 500, LoopbackHarness.FastStabilization());

    reader.Dispose();
    Should.NotThrow(() => reader.Dispose());
  }

  [Fact]
  public void Read_ShouldThrowObjectDisposedException_WhenAlreadyDisposed() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var reader = new StabilizedSocketReader(pair.Server, 500, LoopbackHarness.FastStabilization());
    reader.Dispose();

    Should.Throw<ObjectDisposedException>(() => reader.Read());
  }
}
