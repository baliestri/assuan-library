// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Tests.Platform.TestHelpers;

internal static class LoopbackHarness {
  /// <summary>
  ///   Mutates and returns the shared <see cref="StabilizationOptions.Default" /> singleton with short delay/poll
  ///   values for fast tests. Only safe because all Platform-area tests share the <see cref="PlatformTransportCollection" />,
  ///   which disables parallelization.
  /// </summary>
  internal static StabilizationOptions FastStabilization() {
    var options = StabilizationOptions.Default;
    options.Delay = TimeSpan.FromMilliseconds(30);
    options.PollInterval = TimeSpan.FromMilliseconds(5);
    return options;
  }

  internal static int GetFreeTcpPort() {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
  }

  internal static TcpPair CreateTcpPair() {
    var port = GetFreeTcpPort();
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)port);

    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();

    var client = new TcpClient();
    client.Connect(IPAddress.Loopback, port);

    var server = listener.AcceptTcpClient();
    listener.Stop();

    return new TcpPair(server, client, endpoint);
  }

  internal static UnixSocketPair CreateUnixSocketPair() {
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);

    // The client is explicitly bound to its own path before connecting. An unbound (anonymous)
    // client would make the OS report an empty peer-address name to Socket.Accept(), which the
    // AssuanLibrary.Platform.Unix.Polyfills.UnixDomainSocketEndPoint.Create(SocketAddress) polyfill
    // fails to handle (empty path fails its IsNullOrWhiteSpace guard) - binding the client sidesteps
    // that entirely so Accept() never needs to resolve an anonymous remote endpoint.
    var clientPath = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var clientEndpoint = new UnixDomainSocketEndpoint(clientPath);

    var listenSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    listenSocket.Bind(endpoint);
    listenSocket.Listen(1);

    var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    client.Bind(clientEndpoint);
    client.Connect(endpoint);

    var server = listenSocket.Accept();
    listenSocket.Close();

    if (File.Exists(clientPath)) {
      File.Delete(clientPath);
    }

    return new UnixSocketPair(server, client, endpoint, path);
  }

  internal static NamedPipePair CreateNamedPipePair() {
    var name = $"assuan-test-{Guid.NewGuid():N}";
    var endpoint = new NamedPipeEndpoint(".", name);

    var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.None);
    var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.None);

    var connectTask = Task.Run(() => server.WaitForConnection());
    client.Connect(2000);
    connectTask.Wait(2000);

    server.ReadMode = PipeTransmissionMode.Message;
    client.ReadMode = PipeTransmissionMode.Message;

    return new NamedPipePair(server, client, endpoint);
  }
}

internal readonly record struct TcpPair(TcpClient Server, TcpClient Client, TcpClientEndpoint Endpoint) : IDisposable {
  public void Dispose() {
    Server.Dispose();
    Client.Dispose();
  }
}

internal readonly record struct UnixSocketPair(Socket Server, Socket Client, UnixDomainSocketEndpoint Endpoint, string SocketPath) : IDisposable {
  public void Dispose() {
    Server.Dispose();
    Client.Dispose();

    if (File.Exists(SocketPath)) {
      File.Delete(SocketPath);
    }
  }
}

internal readonly record struct NamedPipePair(NamedPipeServerStream Server, NamedPipeClientStream Client, NamedPipeEndpoint Endpoint) : IDisposable {
  public void Dispose() {
    Server.Dispose();
    Client.Dispose();
  }
}
