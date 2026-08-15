// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Tests.Platform.TestHelpers;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Platform.Unix.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class UnixDomainSocketListenerTests {
  // See UnixDomainSocketConnectionTests.Open_ShouldConnect_WhenListenerAccepts for the underlying
  // production Accept() bug this works around: the test client here must explicitly bind before
  // connecting, otherwise Socket.Accept() throws resolving the peer's anonymous address.

  [Fact]
  public void Endpoint_ShouldReturnConfiguredEndpoint() {
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);
    var listener = new UnixDomainSocketListener(endpoint, AssuanListenerOptions.Default);

    listener.Endpoint.ShouldBe(endpoint);
  }

  [Fact]
  public async Task Accept_ShouldReturnConnectedConnection_WhenClientConnects() {
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);
    var listener = new UnixDomainSocketListener(endpoint, AssuanListenerOptions.Default);

    var clientPath = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var clientEndpoint = new UnixDomainSocketEndpoint(clientPath);

    try {
      var acceptTask = Task.Run(() => listener.Accept());

      await Task.Delay(50);
      var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
      client.Bind(clientEndpoint);
      client.Connect(endpoint);

      var connection = await acceptTask;
      await using var _ = connection;

      connection.IsConnected.ShouldBeTrue();

      client.Send("hello"u8);
      var result = connection.Read();
      result.ShouldBe("hello"u8.ToArray());

      client.Dispose();
    }
    finally {
      if (File.Exists(path)) {
        File.Delete(path);
      }

      if (File.Exists(clientPath)) {
        File.Delete(clientPath);
      }
    }
  }
}
