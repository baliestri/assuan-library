// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Exceptions;
using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Protocol;
using AssuanLibrary.Tests.Platform.TestHelpers;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Platform.Common.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class TcpClientConnectionTests {
  private static AssuanConnectionOptions FastConnectionOptions()
    => new() { TimeoutInMilliseconds = 300 };

  [Fact]
  public void IsConnected_ShouldBeTrue_WhenConstructedWithLiveTransport() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.IsConnected.ShouldBeTrue();
  }

  [Fact]
  public async Task Open_ShouldConnect_WhenListenerAccepts() {
    var port = LoopbackHarness.GetFreeTcpPort();
    var endpoint = new AssuanLibrary.Platform.Common.Transport.Endpoints.TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)port);

    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
    listener.Start();

    using var connection = new TcpClientConnection(endpoint, FastConnectionOptions());

    var acceptTask = Task.Run(() => listener.AcceptTcpClient());
    connection.Open();
    using var accepted = await acceptTask;
    listener.Stop();

    connection.IsConnected.ShouldBeTrue();
  }

  [Fact]
  public async Task OpenAsync_ShouldConnect_WhenListenerAccepts() {
    var port = LoopbackHarness.GetFreeTcpPort();
    var endpoint = new AssuanLibrary.Platform.Common.Transport.Endpoints.TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)port);

    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
    listener.Start();

    await using var connection = new TcpClientConnection(endpoint, FastConnectionOptions());

    var acceptTask = listener.AcceptTcpClientAsync();
    await connection.OpenAsync();
    using var accepted = await acceptTask;
    listener.Stop();

    connection.IsConnected.ShouldBeTrue();
  }

  [Fact]
  public void Open_ShouldBeNoOp_WhenAlreadyConnected() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    Should.NotThrow(() => connection.Open());
    connection.IsConnected.ShouldBeTrue();
  }

  [Fact]
  public void Write_ShouldSendBytes_ToPeer() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Write("hello"u8.ToArray());

    var buffer = new byte[5];
    var read = pair.Client.GetStream().Read(buffer, 0, buffer.Length);

    read.ShouldBe(5);
    buffer.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public async Task WriteAsync_ShouldSendBytes_ToPeer() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    await connection.WriteAsync("hello"u8.ToArray());

    var buffer = new byte[5];
    var read = await pair.Client.GetStream().ReadAsync(buffer);

    read.ShouldBe(5);
    buffer.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public void Write_ShouldThrowAssuanClientException_WhenNotConnected() {
    var endpoint = new AssuanLibrary.Platform.Common.Transport.Endpoints.TcpClientEndpoint(System.Net.IPAddress.Loopback, 0);
    using var connection = new TcpClientConnection(endpoint, FastConnectionOptions());

    Should.Throw<AssuanClientException>(() => connection.Write("hi"u8.ToArray()));
  }

  [Fact]
  public void Read_ShouldReturnData_FromPeer() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    pair.Client.GetStream().Write("hello"u8);

    var result = connection.Read();

    result.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public async Task ReadAsync_ShouldReturnData_FromPeer() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    await pair.Client.GetStream().WriteAsync("hello"u8.ToArray());

    var result = await connection.ReadAsync();

    result.ToArray().ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public void ReadAvailable_ShouldReturnData_UntilLineFeed() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    pair.Client.GetStream().Write("OK\n"u8);

    var result = connection.ReadAvailable();

    result.ShouldBe("OK\n"u8.ToArray());
  }

  [Fact]
  public void DiscardPendingInput_ShouldDrainOsBuffer() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    pair.Client.GetStream().Write("garbage"u8);
    Thread.Sleep(50);

    connection.DiscardPendingInput();

    pair.Client.GetStream().Write("OK\n"u8);
    var result = connection.ReadAvailable();

    result.ShouldBe("OK\n"u8.ToArray());
  }

  [Fact]
  public async Task Read_WithInquireHandler_ShouldInvokeHandler_OnInquireResponse() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    var inquireBuffer = AssuanResponse.Inquire("PASSPHRASE", "id").GetOriginalBuffer();
    var okBuffer = AssuanResponse.Ok().GetOriginalBuffer();

    var serverTask = Task.Run(() => {
      var stream = pair.Client.GetStream();
      stream.Write(inquireBuffer);

      var buffer = new byte[256];
      var totalRead = 0;
      // read the D line + END sent back by the handler
      while (totalRead < "D secret\nEND\n".Length) {
        var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
        if (read == 0) {
          break;
        }

        totalRead += read;
      }

      stream.Write(okBuffer);
    });

    string? seenKeyword = null;
    var result = connection.Read(ctx => {
      seenKeyword = ctx.Keyword;
      ctx.Write("secret");
      ctx.End();
    });

    await serverTask;

    seenKeyword.ShouldBe("PASSPHRASE");
    // The final buffer includes every line accumulated during the loop, including the INQUIRE
    // line itself, not just the terminal OK.
    result.ShouldBe([..inquireBuffer, ..okBuffer]);
  }

  [Fact]
  public async Task ReadAsync_WithAsyncInquireHandler_ShouldInvokeHandler_OnInquireResponse() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    var inquireBuffer = AssuanResponse.Inquire("PASSPHRASE", "id").GetOriginalBuffer();
    var okBuffer = AssuanResponse.Ok().GetOriginalBuffer();

    var serverTask = Task.Run(async () => {
      var stream = pair.Client.GetStream();
      await stream.WriteAsync(inquireBuffer);

      var buffer = new byte[256];
      var totalRead = 0;
      while (totalRead < "D secret\nEND\n".Length) {
        var read = await stream.ReadAsync(buffer.AsMemory(totalRead));
        if (read == 0) {
          break;
        }

        totalRead += read;
      }

      await stream.WriteAsync(okBuffer);
    });

    string? seenKeyword = null;
    var result = await connection.ReadAsync(async (ctx, ct) => {
      seenKeyword = ctx.Keyword;
      await ctx.WriteAsync("secret", ct);
      await ctx.EndAsync(ct);
    });

    await serverTask;

    seenKeyword.ShouldBe("PASSPHRASE");
    result.ToArray().ShouldBe([..inquireBuffer, ..okBuffer]);
  }

  [Fact]
  public async Task Read_WithInquireHandler_ShouldCancelAndRethrow_WhenHandlerThrows() {
    using var pair = LoopbackHarness.CreateTcpPair();
    using var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    var inquireBuffer = AssuanResponse.Inquire("PASSPHRASE", "id").GetOriginalBuffer();

    var serverTask = Task.Run(() => {
      var stream = pair.Client.GetStream();
      stream.Write(inquireBuffer);

      var buffer = new byte[256];
      var read = stream.Read(buffer, 0, buffer.Length);
      return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    });

    Should.Throw<InvalidOperationException>(() => connection.Read(_ => throw new InvalidOperationException("boom")));

    var received = await serverTask;
    received.ShouldBe("CANCEL\n");
  }

  [Fact]
  public void Close_ShouldDisconnect_AndFlipIsConnectedFalse() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Close();

    connection.IsConnected.ShouldBeFalse();
  }

  [Fact]
  public void Close_ShouldThrowAssuanClientException_WhenCalledTwice() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Close();

    Should.Throw<AssuanClientException>(() => connection.Close());
  }

  [Fact]
  public async Task CloseAsync_ShouldThrowAssuanClientException_WhenCalledTwice() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    await connection.CloseAsync();

    await Should.ThrowAsync<AssuanClientException>(async () => await connection.CloseAsync());
  }

  [Fact]
  public void Close_ShouldReleaseUnderlyingSocket_SoPeerObservesEof() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Close();

    var buffer = new byte[1];
    var read = pair.Client.GetStream().Read(buffer, 0, 1);

    read.ShouldBe(0);
  }

  [Fact]
  public void Dispose_ShouldBeIdempotent_WhenCalledMultipleTimes() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Dispose();
    Should.NotThrow(() => connection.Dispose());
  }

  [Fact]
  public void Write_ShouldThrowObjectDisposedException_WhenDisposed() {
    using var pair = LoopbackHarness.CreateTcpPair();
    var connection = new TcpClientConnection(pair.Server, pair.Endpoint, FastConnectionOptions());
    connection.Dispose();

    Should.Throw<ObjectDisposedException>(() => connection.Write("hi"u8.ToArray()));
  }
}
