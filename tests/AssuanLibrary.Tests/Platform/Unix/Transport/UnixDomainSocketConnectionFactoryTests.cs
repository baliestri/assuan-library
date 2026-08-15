// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Unix.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class UnixDomainSocketConnectionFactoryTests {
  [Fact]
  public void CreateConnection_ShouldReturnUnixDomainSocketConnection_ForUnixEndpoint() {
    var factory = new UnixDomainSocketConnectionFactory(AssuanClientOptions.Default);
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);

    using var connection = factory.CreateConnection(endpoint);

    connection.ShouldBeOfType<UnixDomainSocketConnection>();
  }

  [Fact]
  public void CreateConnection_ShouldThrowNotSupportedException_ForMismatchedEndpoint() {
    var factory = new UnixDomainSocketConnectionFactory(AssuanClientOptions.Default);
    var endpoint = new TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    var ex = Should.Throw<NotSupportedException>(() => factory.CreateConnection(endpoint));
    ex.Message.ShouldContain(typeof(TcpClientEndpoint).FullName!);
  }

  [Fact]
  public void CreateConnection_ShouldInvokeConfigureConnectionCallback() {
    var invoked = false;
    var options = new AssuanClientOptions { ConfigureConnection = _ => invoked = true };
    var factory = new UnixDomainSocketConnectionFactory(options);
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);

    using var connection = factory.CreateConnection(endpoint);

    invoked.ShouldBeTrue();
  }
}
