// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Unix.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class UnixDomainSocketListenerFactoryTests {
  [Fact]
  public void CreateListener_ShouldReturnUnixDomainSocketListener_ForUnixEndpoint() {
    var factory = new UnixDomainSocketListenerFactory(AssuanServerOptions.Default);
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);

    var listener = factory.CreateListener(endpoint);

    listener.ShouldBeOfType<UnixDomainSocketListener>();
    listener.Endpoint.ShouldBe(endpoint);
  }

  [Fact]
  public void CreateListener_ShouldThrowNotSupportedException_ForMismatchedEndpoint() {
    var factory = new UnixDomainSocketListenerFactory(AssuanServerOptions.Default);
    var endpoint = new TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    var ex = Should.Throw<NotSupportedException>(() => factory.CreateListener(endpoint));
    ex.Message.ShouldContain(typeof(TcpClientEndpoint).FullName!);
  }

  [Fact]
  public void CreateListener_ShouldInvokeConfigureListenerCallback() {
    var invoked = false;
    var options = new AssuanServerOptions { ConfigureListener = _ => invoked = true };
    var factory = new UnixDomainSocketListenerFactory(options);
    var path = Path.Combine(Path.GetTempPath(), $"assuan-{Guid.NewGuid():N}.sock");
    var endpoint = new UnixDomainSocketEndpoint(path);

    factory.CreateListener(endpoint);

    invoked.ShouldBeTrue();
  }
}
