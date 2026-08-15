// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Windows.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class NamedPipeConnectionFactoryTests {
  [Fact]
  public void CreateConnection_ShouldReturnNamedPipeConnection_ForNamedPipeEndpoint() {
    var factory = new NamedPipeConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new NamedPipeEndpoint(".", $"assuan-test-{Guid.NewGuid():N}");

    using var connection = factory.CreateConnection(endpoint);

    connection.ShouldBeOfType<NamedPipeConnection>();
  }

  [Fact]
  public void CreateConnection_ShouldThrowNotSupportedException_ForMismatchedEndpoint() {
    var factory = new NamedPipeConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    var ex = Should.Throw<NotSupportedException>(() => factory.CreateConnection(endpoint));
    ex.Message.ShouldContain(typeof(TcpClientEndpoint).FullName!);
  }

  [Fact]
  public void CreateConnection_ShouldInvokeConfigureConnectionCallback() {
    var invoked = false;
    var options = new AssuanClientOptions { ConfigureConnection = _ => invoked = true };
    var factory = new NamedPipeConnectionFactory(options);
    var endpoint = new NamedPipeEndpoint(".", $"assuan-test-{Guid.NewGuid():N}");

    using var connection = factory.CreateConnection(endpoint);

    invoked.ShouldBeTrue();
  }
}
