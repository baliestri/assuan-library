// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Tests.Client.Fakes;
using AssuanLibrary.Tests.Server.Fakes;

namespace AssuanLibrary.Tests.Server;

public sealed class AssuanSessionLoopTests {
  [Fact]
  public void SetActiveInquire_ShouldThrow_WhenAlreadyActive() {
    var conn = new FakeAssuanConnection();

    using var cts = new CancellationTokenSource();
    var session = new FakeServerSession(cts.Token);

    var dispatcher = new FakeCommandDispatcher();
    var ctx = new RecordingServerContext(conn, session);
    var loop = new AssuanSessionLoop(conn, session, ctx, dispatcher);

    var inquire1 = new FakeServerInquireContext("K", []);
    var inquire2 = new FakeServerInquireContext("K2", []);

    loop.SetActiveInquire(inquire1);

    Should.Throw<InvalidOperationException>(() => loop.SetActiveInquire(inquire2));
  }

  [Fact]
  public void Run_ShouldRouteInquireData_AndEnd_ToActiveInquire() {
    var conn = new FakeAssuanConnection();

    using var cts = new CancellationTokenSource();
    var session = new CancelOnRefreshSession(cts);

    var dispatcher = new FakeCommandDispatcher();
    var ctx = new RecordingServerContext(conn, session);

    var loop = new AssuanSessionLoop(conn, session, ctx, dispatcher);

    var inquire = new FakeServerInquireContext("X", []);
    loop.SetActiveInquire(inquire);

    // Data response buffer is "hello" and then END
    conn.ReadBuffers.Enqueue("D hello\nEND\n"u8.ToArray());

    loop.Run();

    inquire.Received.Count.ShouldBe(1);
    inquire.Received[0].ShouldBe("hello"u8.ToArray());
    inquire.CompleteCalls.ShouldBe(1);
    inquire.CancelCalls.ShouldBe(0);

    // should not dispatch commands while inquire is active
    dispatcher.DispatchCalls.ShouldBeEmpty();
  }

  [Fact]
  public void Run_ShouldSendOperationCanceledError_OnCancelResponse() {
    var conn = new FakeAssuanConnection();

    using var cts = new CancellationTokenSource();
    var session = new CancelOnRefreshSession(cts);

    var dispatcher = new FakeCommandDispatcher();
    var ctx = new RecordingServerContext(conn, session);

    var loop = new AssuanSessionLoop(conn, session, ctx, dispatcher);
    var inquire = new FakeServerInquireContext("X", []);
    loop.SetActiveInquire(inquire);

    conn.ReadBuffers.Enqueue("CANCEL\n"u8.ToArray());

    loop.Run();

    inquire.CancelCalls.ShouldBe(1);

    conn.Writes.Count.ShouldBe(1);
    var resp = new AssuanResponse(conn.Writes[0]);
    resp.Type.ShouldBe(AssuanResponseType.Error);
    resp.ToString("G", CultureInfo.InvariantCulture).ShouldContain("Operation canceled");
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
      => throw new NotSupportedException();

    public Task SendResponseAsync(AssuanResponseCollection responseCollection, CancellationToken ct = default) {
      _connection.Write(responseCollection.GetOriginalBuffer());
      return Task.CompletedTask;
    }

    public Task SendResponseAsync(AssuanResponse response, CancellationToken ct = default) {
      _connection.Write(response.GetOriginalBuffer());
      return Task.CompletedTask;
    }

    public Task<ReadOnlyMemory<byte>> InquireAsync(string keyword, IReadOnlyCollection<string> parameters, CancellationToken ct = default)
      => throw new NotSupportedException();
  }

  private sealed class CancelOnRefreshSession : IServerSession {
    private readonly CancellationTokenSource _cts;

    public CancelOnRefreshSession(CancellationTokenSource cts) {
      _cts = cts;
      CancellationToken = cts.Token;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt { get; private set; } = DateTimeOffset.UtcNow;
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();
    public CancellationToken CancellationToken { get; }

    public void RefreshLastActivity() {
      LastActivityAt = DateTimeOffset.UtcNow;
      _cts.Cancel();
    }

    public void Dispose() { }
  }
}
