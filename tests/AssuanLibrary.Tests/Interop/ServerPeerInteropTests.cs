// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Unix;
using AssuanLibrary.Transport.Windows;

namespace AssuanLibrary.Tests.Interop;

public sealed class ServerPeerInteropTests {
  [Fact]
  public void PublicBinaryFixtureHasAnAsciiSafeWireRepresentation() {
    var output = new byte[1000];
    var result = ProtocolEncoder.EncodeDataChunk(new byte[] { 0, (byte)'%' }, output);
    Assert.Equal("D %00%25\n"u8.ToArray(), output[..result.Written]);
  }

  [Fact]
  public async Task IndependentGnuPgPeerCanDriveTheUnixServer() {
    if (!GnuPgFixture.Required) return;
    if (OperatingSystem.IsWindows()) return;
    await using var fixture = new GnuPgFixture(GnuPgTools.FromEnvironment());
    var socket = Path.Combine(fixture.Home, "peer");
    var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(socket));
    var host = CreateBinaryServer();
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var serving = host.ServeAsync(listener, stop.Token);
    try {
      var response = await fixture.RunPeerAsync(socket, "BINARY\nBYE\n", TestContext.Current.CancellationToken);
      Assert.True(response.Contains("D %00%25", StringComparison.Ordinal), "The peer did not observe binary data.");
      await host.WhenDrainedAsync(TestContext.Current.CancellationToken);
    }
    finally {
      await stop.CancelAsync();
      await serving;
      listener.Cleanup();
    }
  }

  [Fact]
  public async Task LiteralTcpPeerObservesPercentEncodedBinaryData() {
    if (!GnuPgFixture.Required) return;
    GnuPgTools.FromEnvironment();
    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    var host = CreateBinaryServer();
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var serving = host.ServeAsync(listener, stop.Token);
    try {
      using var peer = new TcpClient(AddressFamily.InterNetwork);
      await peer.ConnectAsync(listener.Endpoint.Address, listener.Endpoint.Port, TestContext.Current.CancellationToken);
      await AssertWireExchangeAsync(peer.GetStream(), TestContext.Current.CancellationToken);
      await host.WhenDrainedAsync(TestContext.Current.CancellationToken);
    }
    finally {
      await stop.CancelAsync();
      await serving;
    }
  }

  [Fact]
  public async Task WindowsBytePipeAcceptsAnIndependentPeer() {
    if (!GnuPgFixture.Required) return;
    if (!OperatingSystem.IsWindows()) return;
    GnuPgTools.FromEnvironment();
    var name = "assuan-interop-" + Guid.NewGuid().ToString("N");
    await using var listener = PipeAcceptor.Bind(LocalEndpoint.NamedPipe(@"\\.\pipe\" + name));
    var host = CreateBinaryServer();
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    var serving = host.ServeAsync(listener, stop.Token);
    try {
      await using var peer = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
      await peer.ConnectAsync(TestContext.Current.CancellationToken);
      await AssertWireExchangeAsync(peer, TestContext.Current.CancellationToken);
      await host.WhenDrainedAsync(TestContext.Current.CancellationToken);
    }
    finally {
      await stop.CancelAsync();
      await serving;
    }
  }

  private static AssuanServer<object> CreateBinaryServer() {
    var host = new AssuanServer<object>(() => new object(), new ServerOptions(maxSessions: 1));
    host.Register(new BinaryHandler());
    return host;
  }

  private static async Task AssertWireExchangeAsync(Stream stream, CancellationToken token) {
    var greeting = new byte[3];
    await stream.ReadExactlyAsync(greeting, token);
    Assert.Equal("OK\n"u8.ToArray(), greeting);
    await stream.WriteAsync("BINARY\nBYE\n"u8.ToArray(), token);
    var expected = "D %00%25\nOK\nOK\n"u8.ToArray();
    var response = new byte[expected.Length];
    await stream.ReadExactlyAsync(response, token);
    Assert.Equal(expected, response);
  }

  private sealed class BinaryHandler : ICommandHandler<object> {
    public string Name => "BINARY";
    public string Description => "Public fixed binary response";

    public ValueTask HandleAsync(CommandContext<object> context, CancellationToken token)
      => context.SendDataAsync(new byte[] { 0, (byte)'%' }, token);
  }
}
