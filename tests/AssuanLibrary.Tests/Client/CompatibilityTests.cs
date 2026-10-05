// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Client;

public sealed class CompatibilityTests {
  [Fact]
  public async Task LiteralWireVectorPreservesEndAndFullRemoteErrAfterData() {
    var stream = new ScriptedClientStream("# greeting\nS READY now\nOK\n"u8.ToArray(),
      "D hello%0A\nEND\nERR 4294967295 denied\n"u8.ToArray());
    await using var client = await AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken);
    await using var tx = await client.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    using (var data = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken))) {
      Assert.Equal("hello\n"u8.ToArray(), data.PayloadBytes.ToArray());
    }

    using (var end = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken))) {
      Assert.Equal(ClientEventKind.End, end.Kind);
    }

    using (var final = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken))) {
      Assert.Equal(uint.MaxValue, final.RemoteCode);
    }

    var error = Assert.Throws<ClientException>(tx.Finish);
    Assert.Equal(ClientErrorKind.Remote, error.Kind);
    Assert.True(client.IsUsable);
    Assert.Equal("NOP\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Theory]
  [InlineData("BOGUS\n")]
  [InlineData("partial")]
  public async Task MalformedOrPartialGreetingFailsAsProtocolWithoutLeakingInput(string input) {
    var stream = new ScriptedClientStream(Encoding.UTF8.GetBytes(input));
    var error = await Assert.ThrowsAsync<ClientException>(() => AssuanClient.FromStreamAsync(stream,
      cancellationToken: TestContext.Current.CancellationToken));
    Assert.Equal(ClientErrorKind.Protocol, error.Kind);
    Assert.True(stream.Closed);
    Assert.DoesNotContain(input.Trim(), error.ToString());
  }
}
