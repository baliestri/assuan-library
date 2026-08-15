// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Client;
using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Common.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class TcpClientConnectionFactoryTests {
  [Fact]
  public void CreateConnection_ShouldReturnTcpClientConnection_ForTcpEndpoint() {
    var factory = new TcpClientConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    using var connection = factory.CreateConnection(endpoint);

    connection.ShouldBeOfType<TcpClientConnection>();
  }

  [Fact]
  public void CreateConnection_ShouldThrowNotSupportedException_ForMismatchedEndpoint() {
    var factory = new TcpClientConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new NamedPipeEndpoint(".", "unused");

    var ex = Should.Throw<NotSupportedException>(() => factory.CreateConnection(endpoint));
    ex.Message.ShouldContain(typeof(NamedPipeEndpoint).FullName!);
  }

  [Fact]
  public void CreateConnection_ShouldInvokeConfigureConnectionCallback() {
    var invoked = false;
    var options = new AssuanClientOptions { ConfigureConnection = _ => invoked = true };
    var factory = new TcpClientConnectionFactory(options);
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    using var connection = factory.CreateConnection(endpoint);

    invoked.ShouldBeTrue();
  }
}
