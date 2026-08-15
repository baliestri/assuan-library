// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Platform.TestHelpers;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Tests.Platform.Common.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class DefaultListenerFactoryTests {
  [Fact]
  public void CreateListener_ShouldReturnTcpClientListener_ForTcpEndpoint() {
    var factory = new DefaultListenerFactory(AssuanServerOptions.Default);
    var endpoint = new TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    var listener = factory.CreateListener(endpoint);

    listener.ShouldBeOfType<TcpClientListener>();
  }

  [Fact]
  public void CreateListener_ShouldReturnUnixDomainSocketListener_ForUnixEndpoint() {
    var factory = new DefaultListenerFactory(AssuanServerOptions.Default);
    var endpoint = new UnixDomainSocketEndpoint(Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock"));

    var listener = factory.CreateListener(endpoint);

    listener.ShouldBeOfType<UnixDomainSocketListener>();
  }

  [Fact]
  public void CreateListener_ShouldReturnNamedPipeListener_ForNamedPipeEndpoint() {
    var factory = new DefaultListenerFactory(AssuanServerOptions.Default);
    var endpoint = new NamedPipeEndpoint(".", $"assuan-test-{Guid.NewGuid():N}");

    var listener = factory.CreateListener(endpoint);

    listener.ShouldBeOfType<NamedPipeListener>();
  }

  [Fact]
  public void CreateListener_ShouldThrowNotSupportedException_ForUnknownEndpointType() {
    var factory = new DefaultListenerFactory(AssuanServerOptions.Default);

    Should.Throw<NotSupportedException>(() => factory.CreateListener(new FakeEndpoint()));
  }

  private readonly struct FakeEndpoint : IAssuanEndpoint;
}
