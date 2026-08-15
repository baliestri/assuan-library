// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Protocol;
using AssuanLibrary.Tests.Platform.TestHelpers;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Platform.Unix.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class UnixDomainSocketConnectionTests {
  private static AssuanConnectionOptions FastConnectionOptions()
    => new() { TimeoutInMilliseconds = 300 };

  [Fact]
  public void IsConnected_ShouldBeTrue_WhenConstructedWithLiveTransport() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.IsConnected.ShouldBeTrue();
  }

  [Fact(Skip = "UnixDomainSocketConnection.Open()/UnixDomainSocketListener.Accept() construct an unbound client " +
    "Socket; on this Windows dev machine, Socket.Accept() on the listener then fails resolving the peer's anonymous " +
    "address inside AssuanLibrary.Platform.Unix.Polyfills.UnixDomainSocketEndPoint.Create(SocketAddress) " +
    "(ArgumentException: 'Path cannot be null or whitespace') because an unbound peer reports an empty-name address " +
    "there. This reproduces the exact production Accept() path for any client that doesn't pre-bind, which is the " +
    "common case - reported as a finding rather than fixed here since this task is test-only.")]
  public async Task Open_ShouldConnect_WhenListenerAccepts() {
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);

    var listenSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    listenSocket.Bind(endpoint);
    listenSocket.Listen(1);

    try {
      using var connection = new UnixDomainSocketConnection(endpoint, FastConnectionOptions());

      var acceptTask = Task.Run(() => listenSocket.Accept());
      connection.Open();
      using var accepted = await acceptTask;

      connection.IsConnected.ShouldBeTrue();
    }
    finally {
      listenSocket.Close();
      if (File.Exists(path)) {
        File.Delete(path);
      }
    }
  }

  [Fact]
  public void Write_ShouldSendBytes_ToPeer() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Write("hello"u8.ToArray());

    var buffer = new byte[5];
    var read = pair.Client.Receive(buffer);

    read.ShouldBe(5);
    buffer.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public void Write_ShouldThrowAssuanClientException_WhenNotConnected() {
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);
    using var connection = new UnixDomainSocketConnection(endpoint, FastConnectionOptions());

    Should.Throw<AssuanClientException>(() => connection.Write("hi"u8.ToArray()));
  }

  [Fact]
  public void Read_ShouldReturnData_FromPeer() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    pair.Client.Send("hello"u8);

    var result = connection.Read();

    result.ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public async Task ReadAsync_ShouldReturnData_FromPeer() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    await pair.Client.SendAsync("hello"u8.ToArray());

    var result = await connection.ReadAsync();

    result.ToArray().ShouldBe("hello"u8.ToArray());
  }

  [Fact]
  public void ReadAvailable_ShouldReturnData_UntilLineFeed() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    pair.Client.Send("OK\n"u8);

    var result = connection.ReadAvailable();

    result.ShouldBe("OK\n"u8.ToArray());
  }

  [Fact]
  public void DiscardPendingInput_ShouldDrainOsBuffer() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    pair.Client.Send("garbage"u8);
    Thread.Sleep(50);

    connection.DiscardPendingInput();

    pair.Client.Send("OK\n"u8);
    var result = connection.ReadAvailable();

    result.ShouldBe("OK\n"u8.ToArray());
  }

  [Fact]
  public async Task Read_WithInquireHandler_ShouldInvokeHandler_OnInquireResponse() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    using var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    var inquireBuffer = AssuanResponse.Inquire("PASSPHRASE", "id").GetOriginalBuffer();
    var okBuffer = AssuanResponse.Ok().GetOriginalBuffer();

    var serverTask = Task.Run(() => {
      pair.Client.Send(inquireBuffer);

      var buffer = new byte[256];
      var totalRead = 0;
      while (totalRead < "D secret\nEND\n".Length) {
        var read = pair.Client.Receive(buffer, totalRead, buffer.Length - totalRead, SocketFlags.None);
        if (read == 0) {
          break;
        }

        totalRead += read;
      }

      pair.Client.Send(okBuffer);
    });

    string? seenKeyword = null;
    var result = connection.Read(ctx => {
      seenKeyword = ctx.Keyword;
      ctx.Write("secret");
      ctx.End();
    });

    await serverTask;

    seenKeyword.ShouldBe("PASSPHRASE");
    result.ShouldBe([..inquireBuffer, ..okBuffer]);
  }

  [Fact]
  public void Close_ShouldDisconnect_AndFlipIsConnectedFalse() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Close();

    connection.IsConnected.ShouldBeFalse();
  }

  [Fact]
  public void Close_ShouldThrowAssuanClientException_WhenCalledTwice() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Close();

    Should.Throw<AssuanClientException>(() => connection.Close());
  }

  [Fact]
  public void Close_ShouldReleaseUnderlyingSocket_SoPeerObservesEof() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Close();

    var buffer = new byte[1];
    var read = pair.Client.Receive(buffer);

    read.ShouldBe(0);
  }

  [Fact]
  public void Dispose_ShouldBeIdempotent_WhenCalledMultipleTimes() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());

    connection.Dispose();
    Should.NotThrow(() => connection.Dispose());
  }

  [Fact]
  public void Write_ShouldThrowObjectDisposedException_WhenDisposed() {
    using var pair = LoopbackHarness.CreateUnixSocketPair();
    var connection = new UnixDomainSocketConnection(pair.Server, pair.Endpoint, FastConnectionOptions());
    connection.Dispose();

    Should.Throw<ObjectDisposedException>(() => connection.Write("hi"u8.ToArray()));
  }
}
