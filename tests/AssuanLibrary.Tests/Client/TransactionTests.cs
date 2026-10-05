// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class TransactionTests {
  [Fact]
  public async Task DecodesBorrowedDataAndKeepsEndDistinctFromFinal() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(),
      "D hello%0A\nS PROGRESS 1\n# info\n\nEND\nOK ready\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("GET", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    var token = TestContext.Current.CancellationToken;
    var data = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(token));
    Assert.Equal(ClientEventKind.Data, data.Kind);
    Assert.Equal("hello\n"u8.ToArray(), data.PayloadBytes.ToArray());
    await Assert.ThrowsAsync<InvalidOperationException>(() => tx.ReadNextAsync(token));
    Assert.Throws<InvalidOperationException>(tx.Finish);
    var borrowed = data.PayloadBytes;
    data.Dispose();
    Assert.Equal(new byte[6], borrowed.ToArray());
    using (var status = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.Status, status.Kind);
      Assert.Equal("PROGRESS"u8.ToArray(), status.KeywordBytes.ToArray());
      Assert.Equal("1"u8.ToArray(), status.PayloadBytes.ToArray());
    }

    using (var comment = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.Comment, comment.Kind);
      Assert.Equal(" info"u8.ToArray(), comment.PayloadBytes.ToArray());
    }

    using (var end = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.End, end.Kind);
    }

    using (var final = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(token))) {
      Assert.Equal(ClientEventKind.Finished, final.Kind);
      Assert.Null(final.RemoteCode);
      Assert.Equal("ready"u8.ToArray(), final.PayloadBytes.ToArray());
    }

    Assert.Null(await tx.ReadNextAsync(token));
    tx.Finish();
    Assert.True(client.IsUsable);
    Assert.Equal("GET\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task SecretClassificationRequiresExplicitExposureAndRedactsDiagnostics() {
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(), "D SECRET_MARKER%00\nOK\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("GET", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Secret, TestContext.Current.CancellationToken);
    var data = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Throws<InvalidOperationException>(() => _ = data.PayloadBytes);
    Assert.Equal("SECRET_MARKER\0"u8.ToArray(), data.ExposeSecret().ToArray());
    Assert.DoesNotContain("SECRET_MARKER", data.ToString());
    data.Dispose();
    var final = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
    final.Dispose();
    tx.Finish();
  }
}
