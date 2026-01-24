// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;
using AssuanLibrary.Network.Utility;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Network.Utility;

[TestSubject(typeof(StabilizedSocketReader))]
public sealed class StabilizedSocketReaderTests {
  private static async Task<(Socket client, Socket server)> CreateConnectedSocketsAsync() {
    var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    listener.Listen(1);

    var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    var connectTask = client.ConnectAsync(listener.LocalEndPoint!);
    var server = await listener.AcceptAsync();

    await connectTask;
    listener.Dispose();

    return (client, server);
  }

  [Test]
  public async Task Read_ShouldReturnAllWrittenData() {
    var (client, server) = await CreateConnectedSocketsAsync();

    var reader = new StabilizedSocketReader(client, TimeSpan.FromSeconds(2));

    var payload = "HELLO"u8.ToArray();

    server.Send(payload);

    var result = await reader.ReadAsync();

    result.ShouldBe(payload);

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldHandleChunkedWrites() {
    var (client, server) = await CreateConnectedSocketsAsync();

    var reader = new StabilizedSocketReader(client, TimeSpan.FromSeconds(2)) {
      StabilizationDelay = TimeSpan.FromMilliseconds(100)
    };

    server.Send("HELLO "u8.ToArray());
    await Task.Delay(50);
    server.Send("WORLD"u8.ToArray());

    var result = await reader.ReadAsync();

    Encoding.ASCII.GetString(result).ShouldBe("HELLO WORLD");

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task ReadAsync_ShouldReturnAllWrittenData() {
    var (client, server) = await CreateConnectedSocketsAsync();

    var reader = new StabilizedSocketReader(client, TimeSpan.FromSeconds(2));

    var payload = "ASYNC"u8.ToArray();

    await server.SendAsync(payload, SocketFlags.None);

    var result = await reader.ReadAsync();

    result.ShouldBe(payload);

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldResetInternalStateBetweenCalls() {
    var (client, server) = await CreateConnectedSocketsAsync();

    var reader = new StabilizedSocketReader(client, TimeSpan.FromSeconds(2));

    server.Send("ONE"u8.ToArray());
    Encoding.ASCII.GetString(await reader.ReadAsync()).ShouldBe("ONE");

    server.Send("TWO"u8.ToArray());
    Encoding.ASCII.GetString(await reader.ReadAsync()).ShouldBe("TWO");

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldReturnEmpty_WhenNoDataArrives() {
    var (client, server) = await CreateConnectedSocketsAsync();

    var reader = new StabilizedSocketReader(client, TimeSpan.FromMilliseconds(300)) {
      StabilizationDelay = TimeSpan.FromMilliseconds(100),
      PollInterval = TimeSpan.FromMilliseconds(30)
    };

    var result = await reader.ReadAsync();

    result.ShouldBeEmpty();

    client.Dispose();
    server.Dispose();
  }

  [Test]
  public async Task Read_ShouldThrowTimeout_WhenStabilizationNeverOccurs() {
    var (client, server) = await CreateConnectedSocketsAsync();

    // Continuously send data so stabilization never happens
    _ = Task.Run(async () => {
      while (true) {
        await server.SendAsync(new byte[] { 1 }, SocketFlags.None);
        await Task.Delay(20);
      }
    });

    var reader = new StabilizedSocketReader(
      client,
      TimeSpan.FromMilliseconds(200)) {
      PollInterval = TimeSpan.FromMilliseconds(20)
    };

    Should.Throw<TimeoutException>(() => reader.Read());

    client.Dispose();
    server.Dispose();
  }
}
