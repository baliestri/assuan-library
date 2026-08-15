// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using AssuanLibrary.Platform.Common.Transport.IO;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Common.Transport.IO;

[Collection(nameof(PlatformTransportCollection))]
public sealed class StabilizedTcpClientReaderTests {
  [Fact]
  public void Read_ShouldReturnData_WhenDataIsImmediatelyAvailable() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var reader = new StabilizedTcpClientReader(pair.Server, 500, LoopbackHarness.FastStabilization());

    pair.Client.GetStream().Write("hello"u8);

    var result = reader.Read();

    result.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public async Task ReadAsync_ShouldReturnData_WhenDataIsImmediatelyAvailable() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var reader = new StabilizedTcpClientReader(pair.Server, 500, LoopbackHarness.FastStabilization());

    await pair.Client.GetStream().WriteAsync("hello"u8.ToArray());

    var result = await reader.ReadAsync();

    result.ToArray().ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public void Read_ShouldThrowTimeoutException_WhenNoDataArrivesBeforeTimeout() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var reader = new StabilizedTcpClientReader(pair.Server, 100, LoopbackHarness.FastStabilization());

    Should.Throw<TimeoutException>(() => reader.Read());
  }

  [Fact]
  public async Task ReadAsync_ShouldThrowTimeoutException_WhenNoDataArrivesBeforeTimeout() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var reader = new StabilizedTcpClientReader(pair.Server, 100, LoopbackHarness.FastStabilization());

    await Should.ThrowAsync<TimeoutException>(async () => await reader.ReadAsync());
  }

  [Fact]
  public async Task Read_ShouldWaitForStabilization_WhenDataArrivesInTrickledChunks() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var options = LoopbackHarness.FastStabilization();
    using var reader = new StabilizedTcpClientReader(pair.Server, 2000, options);

    var stream = pair.Client.GetStream();

    var writerTask = Task.Run(() => {
      stream.Write("one-"u8);
      Thread.Sleep((int)(options.Delay.TotalMilliseconds / 2));
      stream.Write("two"u8);
    });

    var sw = Stopwatch.StartNew();
    var result = reader.Read();
    sw.Stop();

    await writerTask;

    result.ShouldBe("one-two"u8.ToArray());
    sw.Elapsed.ShouldBeGreaterThanOrEqualTo(options.Delay - TimeSpan.FromMilliseconds(15));
  }

  [Fact]
  public async Task ReadAsync_ShouldWaitForStabilization_WhenDataArrivesInTrickledChunks() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var options = LoopbackHarness.FastStabilization();
    using var reader = new StabilizedTcpClientReader(pair.Server, 2000, options);

    var stream = pair.Client.GetStream();

    var writerTask = Task.Run(async () => {
      await stream.WriteAsync("one-"u8.ToArray());
      await Task.Delay((int)(options.Delay.TotalMilliseconds / 2));
      await stream.WriteAsync("two"u8.ToArray());
    });

    var result = await reader.ReadAsync();
    await writerTask;

    result.ToArray().ShouldBe("one-two"u8.ToArray());
  }

  [Fact]
  public void Read_ShouldReturnPartialBuffer_WhenTcpClientDisconnects() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var reader = new StabilizedTcpClientReader(pair.Server, 2000, LoopbackHarness.FastStabilization());

    pair.Client.GetStream().Write("partial"u8);
    pair.Client.Close();

    var result = reader.Read();

    result.ShouldBe("partial"u8.ToArray());
  }

  [Fact]
  public async Task ReadAsync_ShouldCompletePromptly_WhenCancelledDuringWait() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var reader = new StabilizedTcpClientReader(pair.Server, 5000, LoopbackHarness.FastStabilization());

    using var cts = new CancellationTokenSource();
    cts.CancelAfter(30);

    var sw = Stopwatch.StartNew();
    try {
      await reader.ReadAsync(cts.Token);
    }
    catch (OperationCanceledException) {
      // expected on some code paths
    }

    sw.Stop();

    sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
  }

  [Fact]
  public void Dispose_ShouldBeIdempotent_WhenCalledMultipleTimes() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var reader = new StabilizedTcpClientReader(pair.Server, 500, LoopbackHarness.FastStabilization());

    reader.Dispose();
    Should.NotThrow(() => reader.Dispose());
  }

  [Fact]
  public void Read_ShouldThrowObjectDisposedException_WhenAlreadyDisposed() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var reader = new StabilizedTcpClientReader(pair.Server, 500, LoopbackHarness.FastStabilization());
    reader.Dispose();

    Should.Throw<ObjectDisposedException>(() => reader.Read());
  }
}
