// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Server.Dispatching;
using AssuanLibrary.Tests.Client.Fakes;
using AssuanLibrary.Tests.Server.Fakes;

namespace AssuanLibrary.Tests.Server;

public sealed class CommandDispatcherTests {
  [Fact]
  public void Dispatch_ShouldReturnUnknownCommandError_WhenNoHandlerMatched() {
    var dispatcher = new CommandDispatcher([]);

    var conn = new FakeAssuanConnection();
    var session = new FakeServerSession(CancellationToken.None);
    var ctx = new RecordingServerContext(conn, session);

    dispatcher.Dispatch(new AssuanCommand("NOPE"), ctx);

    conn.Writes.Count.ShouldBe(1);
    var response = new AssuanResponse(conn.Writes[0]);
    response.Type.ShouldBe(AssuanResponseType.Error);
    response.ToString("G", CultureInfo.InvariantCulture).ShouldContain("Unknown command");
  }

  [Fact]
  public void Dispatch_ShouldBeCaseInsensitive() {
    var handler = new TestHandler();
    var dispatcher = new CommandDispatcher(new HashSet<CommandHandler> { handler });

    var conn = new FakeAssuanConnection();
    var session = new FakeServerSession(CancellationToken.None);
    var ctx = new RecordingServerContext(conn, session);

    dispatcher.Dispatch(new AssuanCommand("test"), ctx);

    handler.HandleCalls.ShouldBe(1);
    var response = new AssuanResponse(conn.Writes[0]);
    response.Type.ShouldBe(AssuanResponseType.Ok);
  }

  [Fact]
  public void TryAdd_ShouldReturnFalse_WhenDuplicateName() {
    var dispatcher = new CommandDispatcher([]);

    dispatcher.TryAdd(new TestHandler()).ShouldBeTrue();
    dispatcher.TryAdd(new TestHandler()).ShouldBeFalse();
  }

  private sealed class RecordingServerContext : IServerContext {
    private readonly FakeAssuanConnection _connection;

    public RecordingServerContext(FakeAssuanConnection connection, IServerSession session) {
      _connection = connection;
      Session = session;
    }

    public IServerSession Session { get; }

    public void SendResponse(AssuanResponseCollection responseCollection)
      => _connection.Write(responseCollection.GetOriginalBuffer());

    public void SendResponse(AssuanResponse response)
      => _connection.Write(response.GetOriginalBuffer());

    public byte[] Inquire(string keyword, IReadOnlyCollection<string> parameters)
      => throw new NotSupportedException("Not needed for these tests.");

    public Task SendResponseAsync(AssuanResponseCollection responseCollection, CancellationToken ct = default) {
      _connection.Write(responseCollection.GetOriginalBuffer());
      return Task.CompletedTask;
    }

    public Task SendResponseAsync(AssuanResponse response, CancellationToken ct = default) {
      _connection.Write(response.GetOriginalBuffer());
      return Task.CompletedTask;
    }

    public Task<ReadOnlyMemory<byte>> InquireAsync(string keyword, IReadOnlyCollection<string> parameters, CancellationToken ct = default)
      => throw new NotSupportedException("Not needed for these tests.");
  }

  private sealed class TestHandler : CommandHandler {
    public override string Name => "TEST";

    public int HandleCalls { get; private set; }

    public override void Handle(IReadOnlyAssuanCommand command, IServerContext serverContext) {
      HandleCalls++;
      serverContext.SendResponse(AssuanResponse.Ok("handled"));
    }
  }
}
