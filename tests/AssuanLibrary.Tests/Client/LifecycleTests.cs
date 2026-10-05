// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class LifecycleTests {
  [Fact]
  public async Task RemoteErrAfterDataIsRecoverableWithFullUnsignedCode() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(),
      "D x\nERR 4294967295 denied\n"u8.ToArray(), "OK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    await using (var first = await client.CommandAsync("ONE", ReadOnlyMemory<byte>.Empty,
                   PayloadSensitivity.Public, TestContext.Current.CancellationToken)) {
      var data = Assert.IsType<ClientEventLease>(await first.ReadNextAsync(TestContext.Current.CancellationToken));
      data.Dispose();
      var final = Assert.IsType<ClientEventLease>(await first.ReadNextAsync(TestContext.Current.CancellationToken));
      Assert.Equal(uint.MaxValue, final.RemoteCode);
      final.Dispose();
      var error = Assert.Throws<ClientException>(first.Finish);
      Assert.Equal(ClientErrorKind.Remote, error.Kind);
      Assert.Equal(uint.MaxValue, error.RemoteCode);
      Assert.DoesNotContain("denied", error.ToString());
    }

    Assert.True(client.IsUsable);
    await using var second = await client.CommandAsync("TWO", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    var ok = Assert.IsType<ClientEventLease>(await second.ReadNextAsync(TestContext.Current.CancellationToken));
    ok.Dispose();
    second.Finish();
    Assert.Equal("ONE\nTWO\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task PrematureFinishClosesConnection() {
    var early = new ScriptedClientStream("OK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(early,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    Assert.Equal(ClientErrorKind.Incomplete, Assert.Throws<ClientException>(tx.Finish).Kind);
    Assert.False(client.IsUsable);
  }

  [Fact]
  public async Task DuplicateBufferedFinalPreventsANewCommandWithoutSendingBytes() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(), "OK\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    await using (var first = await client.CommandAsync("ONE", ReadOnlyMemory<byte>.Empty,
                   PayloadSensitivity.Public, TestContext.Current.CancellationToken)) {
      var final = Assert.IsType<ClientEventLease>(await first.ReadNextAsync(TestContext.Current.CancellationToken));
      final.Dispose();
      first.Finish();
    }

    var error = await Assert.ThrowsAsync<ClientException>(() => client.CommandAsync("TWO", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Protocol, error.Kind);
    Assert.Equal("ONE\n"u8.ToArray(), stream.WrittenBytes);
    Assert.False(client.IsUsable);
  }

  [Fact]
  public async Task DisposalAfterFinalEventIsSafeEvenWithoutCallingFinish() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(), "OK\n"u8.ToArray(), "OK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    await using (var first = await client.CommandAsync("ONE", ReadOnlyMemory<byte>.Empty,
                   PayloadSensitivity.Public, TestContext.Current.CancellationToken)) {
      using var final = Assert.IsType<ClientEventLease>(await first.ReadNextAsync(TestContext.Current.CancellationToken));
    }

    Assert.True(client.IsUsable);
    await using var second = await client.CommandAsync("TWO", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    var next = Assert.IsType<ClientEventLease>(await second.ReadNextAsync(TestContext.Current.CancellationToken));
    next.Dispose();
    second.Finish();
  }
}
