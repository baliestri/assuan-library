// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Tests.Client.Fakes;

namespace AssuanLibrary.Tests.Client;

public sealed class AssuanClientCommandInvokerTests {
  private static readonly AssuanClientCommandInvoker Invoker = new(new AssuanCommandFormatter(), new AssuanResponseParser());

  [Fact]
  public void Invoke_ShouldWriteCommand_AndParseResponse() {
    var connection = new FakeAssuanConnection();
    connection.ReadBuffers.Enqueue("OK done\n"u8.ToArray());

    var command = new AssuanCommand("CMD");
    command.Add("x");

    var responses = Invoker.Invoke(connection, command);

    connection.Writes.Count.ShouldBe(1);
    connection.Writes[0].ShouldBe(command.ToBytes());
    responses.Count.ShouldBe(1);
    responses[0].Type.ShouldBe(AssuanResponseType.Ok);
  }

  [Fact]
  public void Invoke_ShouldForwardInquireHandler() {
    var connection = new FakeAssuanConnection();
    connection.ReadBuffers.Enqueue("OK done\n"u8.ToArray());

    var command = new AssuanCommand("CMD");

    void Handler(IClientInquireContext _context) { }

    _ = Invoker.Invoke(connection, command, Handler);

    connection.LastInquireHandler.ShouldBe((InquireHandler)Handler);
  }

  [Fact]
  public async Task InvokeAsync_ShouldWriteCommand_AndParseResponse() {
    var connection = new FakeAssuanConnection();
    connection.ReadMemoryBuffers.Enqueue("OK async\n"u8.ToArray().AsMemory());

    var command = new AssuanCommand("CMD");
    command.Add("async");

    var responses = await Invoker.InvokeAsync(connection, command, CancellationToken.None);

    connection.Writes.Count.ShouldBe(1);
    connection.Writes[0].ShouldBe(command.ToBytes());
    responses.Count.ShouldBe(1);
    responses[0].Type.ShouldBe(AssuanResponseType.Ok);
  }

  [Fact]
  public async Task InvokeAsync_ShouldForwardAsyncInquireHandler() {
    var connection = new FakeAssuanConnection();
    connection.ReadMemoryBuffers.Enqueue("OK done\n"u8.ToArray().AsMemory());

    var command = new AssuanCommand("CMD");

    Task<ReadOnlyMemory<byte>> Handler(IClientInquireContext context, CancellationToken ct)
      => Task.FromResult(ReadOnlyMemory<byte>.Empty);

    _ = await Invoker.InvokeAsync(connection, command, Handler, CancellationToken.None);

    connection.LastAsyncInquireHandler.ShouldBe((AsyncInquireHandler)Handler);
  }
}

