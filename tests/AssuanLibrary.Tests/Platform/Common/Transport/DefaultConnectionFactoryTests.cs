// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Tests.Platform.TestHelpers;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Tests.Platform.Common.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class DefaultConnectionFactoryTests {
  [Fact]
  public void CreateConnection_ShouldReturnTcpClientConnection_ForTcpEndpoint() {
    var factory = new DefaultConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    using var connection = factory.CreateConnection(endpoint);

    connection.ShouldBeOfType<TcpClientConnection>();
  }

  [Fact]
  public void CreateConnection_ShouldReturnUnixDomainSocketConnection_ForUnixEndpoint() {
    var factory = new DefaultConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new UnixDomainSocketEndpoint(Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock"));

    using var connection = factory.CreateConnection(endpoint);

    connection.ShouldBeOfType<UnixDomainSocketConnection>();
  }

  [Fact]
  public void CreateConnection_ShouldReturnNamedPipeConnection_ForNamedPipeEndpoint() {
    var factory = new DefaultConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new NamedPipeEndpoint(".", $"assuan-test-{Guid.NewGuid():N}");

    using var connection = factory.CreateConnection(endpoint);

    connection.ShouldBeOfType<NamedPipeConnection>();
  }

  [Fact]
  public void CreateConnection_ShouldThrowNotSupportedException_ForUnknownEndpointType() {
    var factory = new DefaultConnectionFactory(AssuanClientOptions.Default);

    Should.Throw<NotSupportedException>(() => factory.CreateConnection(new FakeEndpoint()));
  }

  private readonly struct FakeEndpoint : IAssuanEndpoint;
}
