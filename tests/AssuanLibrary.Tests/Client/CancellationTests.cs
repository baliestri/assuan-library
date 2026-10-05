// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class CancellationTests {
  [Fact]
  public async Task CancelledReadCannotLeaveAReusableClientOrWipeBorrowedIoEarly() {
    var stream = new DelayedRead();
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var transaction = await client.CommandAsync("GET", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Secret, TestContext.Current.CancellationToken);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var reading = transaction.ReadNextAsync(cancellation.Token);
    await stream._started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    await cancellation.CancelAsync();
    var error = await Assert.ThrowsAsync<ClientException>(() => reading);
    Assert.Equal(ClientErrorKind.Cancelled, error.Kind);
    Assert.False(client.IsUsable);
    Assert.True(stream.Closed);
    stream.Finish();
    Assert.True(SpinWait.SpinUntil(() => stream.Pending.Span.IndexOfAnyExcept((byte)0) < 0,
      TimeSpan.FromSeconds(2)));
  }

  [Fact]
  public async Task CancelledPendingWriteCannotRetryAndKeepsItsBufferUntilCompletion() {
    var stream = new DelayedWrite();
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var command = client.CommandAsync("GET", ReadOnlyMemory<byte>.Empty, PayloadSensitivity.Public,
      cancellation.Token);
    await stream._started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    await cancellation.CancelAsync();
    var error = await Assert.ThrowsAsync<ClientException>(() => command);
    Assert.Equal(ClientErrorKind.Cancelled, error.Kind);
    Assert.False(client.IsUsable);
    Assert.Equal("GET\n"u8.ToArray(), stream.Pending.ToArray());
    stream.Finish();
    Assert.True(SpinWait.SpinUntil(() => stream.Pending.Span.IndexOfAnyExcept((byte)0) < 0,
      TimeSpan.FromSeconds(2)));
    Assert.Empty(stream.WrittenBytes); // No retry or late write via this test stream.
  }

  private sealed class DelayedRead : ScriptedClientStream {
    private readonly TaskCompletionSource<int> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;
    internal DelayedRead() : base("OK\n"u8.ToArray()) { }
    internal Memory<byte> Pending { get; private set; }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) {
      if (Interlocked.Increment(ref _calls) == 1) {
        return base.ReadAsync(buffer, token);
      }

      Pending = buffer;
      _started.TrySetResult();
      return new ValueTask<int>(_release.Task); // Simulates an OS read that ignores cancellation.
    }

    internal void Finish() {
      "D late\n"u8.CopyTo(Pending.Span);
      _release.SetResult(7);
    }
  }

  private sealed class DelayedWrite : ScriptedClientStream {
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal DelayedWrite() : base("OK\n"u8.ToArray()) { }
    internal ReadOnlyMemory<byte> Pending { get; private set; }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) {
      Pending = buffer;
      _started.TrySetResult();
      return new ValueTask(_release.Task); // Deliberately noncooperative.
    }

    internal void Finish() => _release.SetResult();
  }
}
