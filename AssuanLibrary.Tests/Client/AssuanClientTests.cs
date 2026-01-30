// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Protocol;
using AssuanLibrary.Tests.Client.Fakes;
using AssuanLibrary.Transport.Endpoints;
using JetBrains.Annotations;

namespace AssuanLibrary.Tests.Client;

[TestSubject(typeof(AssuanClient))]
public sealed class AssuanClientTests {
  [Fact]
  public void Connect_ShouldOpenConnection_ReadBanner_AndInvokeHooks() {
    var resolver = new FakeEndpointResolver();
    var factory = new FakeAssuanConnectionFactory();

    factory.Connection.ReadBuffers.Enqueue("OK banner"u8.ToArray());

    var calls = new List<string>();
    var options = new AssuanClientOptions {
      OnSessionAuthenticatingAsync = (_, metadata, _) => {
        calls.Add("auth");
        metadata.ShouldContainKeyAndValue("k", 123);
        return Task.CompletedTask;
      },
      OnSessionStartedAsync = (_, metadata, _) => {
        calls.Add("started");
        metadata.ShouldContainKey("banner");
        ((byte[])metadata["banner"]).ShouldBe("OK banner"u8.ToArray());
        return Task.CompletedTask;
      }
    };

    var client = new AssuanClient(resolver, factory, options);

    client.Connect(new TcpClientEndpoint(IPAddress.Loopback, 0), new Dictionary<string, object> { ["k"] = 123 });

    client.IsConnected.ShouldBeTrue();
    factory.Connection.OpenCalls.ShouldBe(1);
    calls.ShouldBe(["auth", "started"]);
  }

  [Fact]
  public void Connect_ShouldNoOp_WhenAlreadyConnected() {
    var resolver = new FakeEndpointResolver();
    var factory = new FakeAssuanConnectionFactory();
    factory.Connection.ReadBuffers.Enqueue("OK banner"u8.ToArray());

    var client = new AssuanClient(resolver, factory, AssuanClientOptions.Default);

    client.Connect(new TcpClientEndpoint(IPAddress.Loopback, 0), new Dictionary<string, object>());
    client.Connect(new TcpClientEndpoint(IPAddress.Loopback, 1), new Dictionary<string, object>());

    factory.Connection.OpenCalls.ShouldBe(1);
  }

  [Fact]
  public void Connect_WithEndpointKind_ShouldUseResolverMetadata() {
    var resolver = new FakeEndpointResolver {
      Resolution = new AssuanEndpointResolution(new TcpClientEndpoint(IPAddress.Loopback, 8080), new Dictionary<string, object> { ["m"] = "v" })
    };
    var factory = new FakeAssuanConnectionFactory();
    factory.Connection.ReadBuffers.Enqueue("OK banner"u8.ToArray());

    IReadOnlyDictionary<string, object>? seenMetadata = null;
    var options = new AssuanClientOptions {
      OnSessionAuthenticatingAsync = (_, metadata, _) => {
        seenMetadata = metadata;
        return Task.CompletedTask;
      }
    };

    var client = new AssuanClient(resolver, factory, options);

    client.Connect(AssuanEndpointKind.AGENT);

    resolver.LastKind.ShouldBe(AssuanEndpointKind.AGENT);
    factory.LastEndpoint.ShouldNotBeNull();
    seenMetadata.ShouldNotBeNull();
    seenMetadata!.ShouldContainKeyAndValue("m", "v");
  }

  [Fact]
  public void Invoke_ShouldReturnEmpty_WhenNotConnected_AndThrowIfNotConnectedIsFalse() {
    var client = new AssuanClient(new FakeEndpointResolver(), new FakeAssuanConnectionFactory(),
      new AssuanClientOptions { ThrowIfNotConnected = false });

    var result = client.Invoke(new AssuanCommand("GETINFO"));

    result.ShouldBeEmpty();
  }

  [Fact]
  public void Invoke_ShouldThrow_WhenNotConnected_AndThrowIfNotConnectedIsTrue() {
    var client = new AssuanClient(new FakeEndpointResolver(), new FakeAssuanConnectionFactory(),
      new AssuanClientOptions { ThrowIfNotConnected = true });

    Should.Throw<AssuanClientException>(() => client.Invoke(new AssuanCommand("GETINFO")));
  }

  [Fact]
  public void Invoke_ShouldWriteCommand_AndReadResponses() {
    var resolver = new FakeEndpointResolver();
    var factory = new FakeAssuanConnectionFactory();
    factory.Connection.ReadBuffers.Enqueue("OK banner"u8.ToArray());

    var client = new AssuanClient(resolver, factory, AssuanClientOptions.Default);
    client.Connect(new TcpClientEndpoint(IPAddress.Loopback, 0), new Dictionary<string, object>());

    factory.Connection.ReadBuffers.Enqueue("OK hi\n"u8.ToArray());

    var cmd = new AssuanCommand("CMD");
    cmd.Add("hi");

    var result = client.Invoke(cmd);

    factory.Connection.Writes.Count.ShouldBe(1);
    result.Count.ShouldBe(1);
    result[0].Type.ShouldBe(AssuanResponseType.Ok);
    result[0].ToString().ShouldBe("hi");
  }

  [Fact]
  public void Invoke_WithInquireHandler_ShouldPassHandlerToConnection() {
    var resolver = new FakeEndpointResolver();
    var factory = new FakeAssuanConnectionFactory();
    factory.Connection.ReadBuffers.Enqueue("OK banner"u8.ToArray());

    var client = new AssuanClient(resolver, factory, AssuanClientOptions.Default);
    client.Connect(new TcpClientEndpoint(IPAddress.Loopback, 0), new Dictionary<string, object>());

    factory.Connection.ReadBuffers.Enqueue("OK done\n"u8.ToArray());

    void Handler(IClientInquireContext _ctx) { }

    _ = client.Invoke(new AssuanCommand("CMD"), Handler);

    factory.Connection.LastInquireHandler.ShouldBe((InquireHandler)Handler);
  }

  [Fact]
  public void Disconnect_ShouldNoOp_WhenNotConnected() {
    var factory = new FakeAssuanConnectionFactory();
    var client = new AssuanClient(new FakeEndpointResolver(), factory, AssuanClientOptions.Default);

    client.Disconnect();

    factory.Connection.CloseCalls.ShouldBe(0);
  }

  [Fact]
  public void Connect_ShouldWrapSocketExceptions() {
    var factory = new FakeAssuanConnectionFactory();
    factory.Connection.ThrowOnOpen = new SocketException((int)SocketError.ConnectionRefused);

    var client = new AssuanClient(new FakeEndpointResolver(), factory, AssuanClientOptions.Default);

    var ex = Should.Throw<AssuanClientException>(()
      => client.Connect(new TcpClientEndpoint(IPAddress.Loopback, 0), new Dictionary<string, object>()));
    ex.InnerException.ShouldBeOfType<SocketException>();
    factory.Connection.DisposeCalls.ShouldBe(1);
  }

  [Fact]
  public async Task ConnectAsync_ShouldWrapExceptions_AndDisposeAsync() {
    var factory = new FakeAssuanConnectionFactory();
    factory.Connection.ThrowOnOpenAsync = new InvalidOperationException("boom");

    var client = new AssuanClient(new FakeEndpointResolver(), factory, AssuanClientOptions.Default);

    var ex = await Should.ThrowAsync<AssuanClientException>(() =>
      client.ConnectAsync(new TcpClientEndpoint(IPAddress.Loopback, 0), new Dictionary<string, object>(), CancellationToken.None));

    ex.InnerException.ShouldBeOfType<InvalidOperationException>();
    factory.Connection.DisposeAsyncCalls.ShouldBe(1);
  }
}
