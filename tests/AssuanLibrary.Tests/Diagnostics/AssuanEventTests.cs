// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Diagnostics;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Client;
using AssuanLibrary.Tests.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Diagnostics;

public sealed class AssuanEventTests {
  [Fact]
  public async Task ServerEmitsOnlyNumericAndEnumCommandOutcomesEvenWhenSinkThrows() {
    var sink = new RecordingSink { Throw = true };
    await using var stream = new SessionDuplexStream("NOP secret-marker\nBYE\n"u8.ToArray());
    var session = new ServerSession<object>(new AcceptedConnection(stream), new object(),
      new CommandRegistry<object>(), new DefaultSessionHooks<object>(),
      new ServerOptions(eventSink: sink));
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal("OK\nOK\nOK\n"u8.ToArray(), stream.WrittenBytes);
    Assert.Contains(sink.Events,
      item => item is { Component: AssuanComponent.Server, Id: AssuanEventId.FinalResponse, Outcome: AssuanOutcome.Success });
    Assert.All(sink.Events, item => {
      Assert.DoesNotContain("secret-marker", item.ToString());
      Assert.DoesNotContain("SECRET_MARKER", item.ToString());
    });
  }

  [Fact]
  public async Task ClientRemoteErrAndFramingFailureRemainTypedWhenSinkThrows() {
    var sink = new RecordingSink { Throw = true };
    var stream = new ScriptedClientStream("OK\n"u8.ToArray(), "ERR 7 SECRET_MARKER\n"u8.ToArray());
    await using (var client = await AssuanClient.FromStreamAsync(stream, new ClientOptions(eventSink: sink),
                   TestContext.Current.CancellationToken)) {
      await using var tx = await client.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
        PayloadSensitivity.Public, TestContext.Current.CancellationToken);
      var final = Assert.IsType<ClientEventLease>(await tx.ReadNextAsync(TestContext.Current.CancellationToken));
      final.Dispose();
      Assert.Equal(ClientErrorKind.Remote, Assert.Throws<ClientException>(tx.Finish).Kind);
    }

    Assert.Contains(sink.Events,
      item => item is { Component: AssuanComponent.Client, Id: AssuanEventId.RemoteFinal, Outcome: AssuanOutcome.RemoteError });
    Assert.All(sink.Events, item => Assert.DoesNotContain("SECRET_MARKER", item.ToString()));

    var eof = new ScriptedClientStream("OK\n"u8.ToArray(), ""u8.ToArray());
    await using var other = await AssuanClient.FromStreamAsync(eof, new ClientOptions(eventSink: sink),
      TestContext.Current.CancellationToken);
    await using var pending = await other.CommandAsync("NOP", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Public, TestContext.Current.CancellationToken);
    await Assert.ThrowsAsync<ClientException>(() => pending.ReadNextAsync(TestContext.Current.CancellationToken));
    Assert.Contains(sink.Events, item => item is { Component: AssuanComponent.Transport, Id: AssuanEventId.ReadFailed });
  }

  private sealed class RecordingSink : IAssuanEventSink {
    internal readonly List<AssuanEvent> Events = [];
    internal bool Throw;

    public void Emit(in AssuanEvent eventInfo) {
      Events.Add(eventInfo);
      if (Throw) {
        throw new IOException("SECRET_MARKER");
      }
    }
  }
}
