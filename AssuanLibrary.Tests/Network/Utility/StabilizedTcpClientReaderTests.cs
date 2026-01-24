// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;
using AssuanLibrary.Network.Utility;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Network.Utility;

[TestSubject(typeof(StabilizedTcpClientReader))]
public sealed class StabilizedTcpClientReaderTests {
  private static async Task<(TcpClient client, TcpClient server)> CreateConnectedClientsAsync() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    var client = new TcpClient();
    var clientConnect = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);

    var server = await listener.AcceptTcpClientAsync();
    await clientConnect;

    listener.Stop();
    return (client, server);
  }

  [Test]
  public async Task Read_ShouldReturnAllWrittenData() {
    var (client, server) = await CreateConnectedClientsAsync();
    var writer = server.GetStream();

    var reader = new StabilizedTcpClientReader(
      client,
      TimeSpan.FromSeconds(2));

    var payload = "HELLO"u8.ToArray();

    await writer.WriteAsync(payload);
    await writer.FlushAsync();

    var result = await reader.ReadAsync();

    result.ShouldBe(payload);

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldHandleChunkedWrites() {
    var (client, server) = await CreateConnectedClientsAsync();
    var writer = server.GetStream();

    var reader = new StabilizedTcpClientReader(
      client,
      TimeSpan.FromSeconds(2)) {
      StabilizationDelay = TimeSpan.FromMilliseconds(100)
    };

    await writer.WriteAsync("HELLO "u8.ToArray());
    await Task.Delay(50);
    await writer.WriteAsync("WORLD"u8.ToArray());
    await writer.FlushAsync();

    var result = await reader.ReadAsync();

    Encoding.ASCII.GetString(result).ShouldBe("HELLO WORLD");

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task ReadAsync_ShouldReturnAllWrittenData() {
    var (client, server) = await CreateConnectedClientsAsync();
    var writer = server.GetStream();

    var reader = new StabilizedTcpClientReader(
      client,
      TimeSpan.FromSeconds(2));

    var payload = "ASYNC"u8.ToArray();

    await writer.WriteAsync(payload);
    await writer.FlushAsync();

    var result = await reader.ReadAsync();

    result.ShouldBe(payload);

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldResetInternalStateBetweenCalls() {
    var (client, server) = await CreateConnectedClientsAsync();
    var writer = server.GetStream();

    var reader = new StabilizedTcpClientReader(
      client,
      TimeSpan.FromSeconds(2));

    await writer.WriteAsync("ONE"u8.ToArray());
    await writer.FlushAsync();

    var first = await reader.ReadAsync();
    Encoding.ASCII.GetString(first).ShouldBe("ONE");

    await writer.WriteAsync("TWO"u8.ToArray());
    await writer.FlushAsync();

    var second = await reader.ReadAsync();
    Encoding.ASCII.GetString(second).ShouldBe("TWO");

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldThrowTimeout_WhenNoDataArrives() {
    var (client, server) = await CreateConnectedClientsAsync();

    var reader = new StabilizedTcpClientReader(client, TimeSpan.FromMilliseconds(100));

    await Should.ThrowAsync<TimeoutException>(async () => await reader.ReadAsync());

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task ReadAsync_ShouldRespectCancellation() {
    var (client, server) = await CreateConnectedClientsAsync();

    var reader = new StabilizedTcpClientReader(client, TimeSpan.FromSeconds(5));

    using var cts = new CancellationTokenSource();
    cts.CancelAfter(100);

    await Should.ThrowAsync<OperationCanceledException>(async () => await reader.ReadAsync(cts.Token));

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldThrowAfterDispose() {
    var (client, server) = await CreateConnectedClientsAsync();

    var reader = new StabilizedTcpClientReader(client, TimeSpan.FromSeconds(1));

    reader.Dispose();

    await Should.ThrowAsync<ObjectDisposedException>(async () => await reader.ReadAsync());

    client.Dispose();
    server.Dispose();
  }
}
