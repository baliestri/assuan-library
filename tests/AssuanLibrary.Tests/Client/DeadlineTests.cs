// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class DeadlineTests {
  [Fact]
  public async Task CallerPauseBetweenEventsConsumesOriginalCommandBudget() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(), "D one\nOK\n"u8.ToArray());
    var clock = new ManualClock();
    await using var client = await AssuanClient.FromStreamWithClockAsync(stream,
      new ClientOptions(commandTimeout: TimeSpan.FromSeconds(2)), clock, TestContext.Current.CancellationToken);
    await using var transaction = await client.CommandAsync("GET", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    using (var data = Assert.IsType<ClientEventLease>(await transaction.ReadNextAsync(TestContext.Current.CancellationToken))) {
      Assert.Equal("one"u8.ToArray(), data.PayloadBytes.ToArray());
    }

    clock.Advance(TimeSpan.FromSeconds(2));
    var error = await Assert.ThrowsAsync<ClientException>(() => transaction.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Timeout, error.Kind);
    Assert.False(client.IsUsable);
  }

  private sealed class ManualClock : TimeProvider {
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
      => Volatile.Read(ref _ticks);

    internal void Advance(TimeSpan amount)
      => Interlocked.Add(ref _ticks, amount.Ticks);
  }
}
