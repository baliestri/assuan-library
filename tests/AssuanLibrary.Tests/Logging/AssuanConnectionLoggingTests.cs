// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Logging;
using AssuanLibrary.Protocol;
using AssuanLibrary.Tests.Client.Fakes;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Logging;

public sealed class AssuanConnectionLoggingTests {
  [Fact]
  public void Invoke_ShouldLogClientTraffic_WithRedactedPayloads() {
    var logger = new RecordingAssuanLogger();
    var inner = new FakeAssuanConnection();
    inner.ReadBuffers.Enqueue("OK secret-response\n"u8.ToArray());

    var connection = new LoggingAssuanConnection(inner, new AssuanLoggingOptions { Logger = logger }, AssuanConnectionLoggingRole.Client);
    var invoker = new AssuanClientCommandInvoker(new AssuanCommandFormatter(), new AssuanResponseParser());
    var command = new AssuanCommand("GETPIN") { "secret-command" };

    _ = invoker.Invoke(connection, command);

    logger.Messages.ShouldContain(message => message.Contains("Client -> Server command GETPIN"));
    logger.Messages.ShouldContain(message => message.Contains("Server -> Client response OK"));
    logger.Messages.ShouldAllBe(message => !message.Contains("secret-command") && !message.Contains("secret-response"));
    logger.Messages.ShouldAllBe(message => message.Contains("payload=<redacted>"));
  }

  [Fact]
  public async Task InvokeAsync_ShouldLogClientTraffic() {
    var logger = new RecordingAssuanLogger();
    var inner = new FakeAssuanConnection();
    inner.ReadMemoryBuffers.Enqueue("OK async-response\n"u8.ToArray().AsMemory());

    var connection = new LoggingAssuanConnection(inner, new AssuanLoggingOptions { Logger = logger }, AssuanConnectionLoggingRole.Client);
    var invoker = new AssuanClientCommandInvoker(new AssuanCommandFormatter(), new AssuanResponseParser());
    var command = new AssuanCommand("GETINFO") { "version" };

    _ = await invoker.InvokeAsync(connection, command, CancellationToken.None);

    logger.Messages.ShouldContain(message => message.Contains("Client -> Server command GETINFO"));
    logger.Messages.ShouldContain(message => message.Contains("Server -> Client response OK"));
  }

  [Fact]
  public void Write_ShouldHonorRawPayloadMode() {
    var logger = new RecordingAssuanLogger();
    var connection = new LoggingAssuanConnection(
      new FakeAssuanConnection(),
      new AssuanLoggingOptions {
        Logger = logger,
        PayloadMode = AssuanPayloadLoggingMode.Raw
      },
      AssuanConnectionLoggingRole.Client
    );

    connection.Write("GETINFO sensitive-value\n"u8.ToArray());

    logger.Messages.Single().ShouldContain("payload=\"GETINFO sensitive-value\"");
  }

  [Fact]
  public void Write_ShouldHonorNoPayloadMode() {
    var logger = new RecordingAssuanLogger();
    var connection = new LoggingAssuanConnection(
      new FakeAssuanConnection(),
      new AssuanLoggingOptions {
        Logger = logger,
        PayloadMode = AssuanPayloadLoggingMode.None
      },
      AssuanConnectionLoggingRole.Client
    );

    connection.Write("GETINFO sensitive-value\n"u8.ToArray());

    logger.Messages.Single().ShouldNotContain("payload=");
    logger.Messages.Single().ShouldNotContain("sensitive-value");
  }

  [Fact]
  public void ReadWithInquireHandler_ShouldLogClientInquireResponses() {
    var logger = new RecordingAssuanLogger();
    var inner = new InquireInvokingConnection();
    var connection = new LoggingAssuanConnection(inner, new AssuanLoggingOptions { Logger = logger }, AssuanConnectionLoggingRole.Client);

    _ = connection.Read(context => {
      context.Write("sensitive-inquire-data");
      context.End();
    });

    logger.Messages.ShouldContain(message => message.Contains("Client -> Server response D"));
    logger.Messages.ShouldContain(message => message.Contains("Client -> Server response END"));
    logger.Messages.ShouldContain(message => message.Contains("Server -> Client response OK"));
    logger.Messages.ShouldAllBe(message => !message.Contains("sensitive-inquire-data"));
  }

  [Fact]
  public void ServerRole_ShouldLogServerAndClientDirections() {
    var logger = new RecordingAssuanLogger();
    var inner = new FakeAssuanConnection();
    inner.ReadBuffers.Enqueue("PING sensitive-command\n"u8.ToArray());

    var connection = new LoggingAssuanConnection(inner, new AssuanLoggingOptions { Logger = logger }, AssuanConnectionLoggingRole.Server);

    connection.Write(AssuanResponse.Ok("ready").GetOriginalBuffer());
    _ = connection.ReadAvailable();

    logger.Messages.ShouldContain(message => message.Contains("Server -> Client response OK"));
    logger.Messages.ShouldContain(message => message.Contains("Client -> Server command PING"));
    logger.Messages.ShouldAllBe(message => !message.Contains("sensitive-command"));
  }

  [Fact]
  public async Task ServerRoleAsync_ShouldLogServerAndClientDirections() {
    var logger = new RecordingAssuanLogger();
    var inner = new FakeAssuanConnection();
    inner.ReadMemoryBuffers.Enqueue("PING async-sensitive-command\n"u8.ToArray().AsMemory());

    var connection = new LoggingAssuanConnection(inner, new AssuanLoggingOptions { Logger = logger }, AssuanConnectionLoggingRole.Server);

    await connection.WriteAsync(AssuanResponse.Ok("ready").GetOriginalBuffer(), CancellationToken.None);
    _ = await connection.ReadAvailableAsync(CancellationToken.None);

    logger.Messages.ShouldContain(message => message.Contains("Server -> Client response OK"));
    logger.Messages.ShouldContain(message => message.Contains("Client -> Server command PING"));
    logger.Messages.ShouldAllBe(message => !message.Contains("async-sensitive-command"));
  }

  [Fact]
  public void NullLogger_ShouldNotAffectConnectionOperations() {
    var connection = new LoggingAssuanConnection(
      new FakeAssuanConnection(),
      new AssuanLoggingOptions { Logger = NullAssuanLogger.Instance },
      AssuanConnectionLoggingRole.Client
    );

    Should.NotThrow(() => connection.Write("GETINFO version\n"u8.ToArray()));
  }

  private sealed class RecordingAssuanLogger : IAssuanLogger {
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
      => null;

    public bool IsEnabled(AssuanLogLevel logLevel)
      => logLevel is not AssuanLogLevel.None;

    public void Log<TState>(AssuanLogLevel logLevel, AssuanEventId eventId, TState state, Exception? exception,
    Func<TState, Exception?, string> formatter)
      => Messages.Add(formatter(state, exception));
  }

  private sealed class InquireInvokingConnection : IAssuanConnection {
    public bool IsConnected => true;

    public void Open() { }

    public void Write(byte[] buffer) { }

    public byte[] Read()
      => "OK done\n"u8.ToArray();

    public byte[] Read(InquireHandler inquireHandler) {
      inquireHandler(new RecordingClientInquireContext());
      return Read();
    }

    public byte[] ReadAvailable()
      => Read();

    public void DiscardPendingInput() { }

    public void Close() { }

    public Task OpenAsync(CancellationToken ct = default)
      => Task.CompletedTask;

    public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
      => Task.CompletedTask;

    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default)
      => new(Read().AsMemory());

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler, CancellationToken ct = default) {
      await inquireHandler(new RecordingClientInquireContext(), ct).ConfigureAwait(false);
      return Read().AsMemory();
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadAvailableAsync(CancellationToken ct = default)
      => ReadAsync(ct);

    public ValueTask DiscardPendingInputAsync(CancellationToken ct = default)
      => ValueTask.CompletedTask;

    public Task CloseAsync(CancellationToken ct = default)
      => Task.CompletedTask;

    public void Dispose() { }

    public ValueTask DisposeAsync()
      => ValueTask.CompletedTask;
  }

  private sealed class RecordingClientInquireContext : IClientInquireContext {
    public string Keyword => "keyword";

    public IReadOnlyCollection<string> Parameters { get; } = [];

    public void Write(string value) { }

    public void Write(byte[] buffer) { }

    public void End() { }

    public void Cancel() { }

    public ValueTask WriteAsync(string value, CancellationToken ct = default)
      => ValueTask.CompletedTask;

    public ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default)
      => ValueTask.CompletedTask;

    public ValueTask EndAsync(CancellationToken ct = default)
      => ValueTask.CompletedTask;

    public ValueTask CancelAsync(CancellationToken ct = default)
      => ValueTask.CompletedTask;
  }
}
