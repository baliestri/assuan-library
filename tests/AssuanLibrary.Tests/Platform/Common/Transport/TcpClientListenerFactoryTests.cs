// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Common.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class TcpClientListenerFactoryTests {
  [Fact]
  public void CreateListener_ShouldReturnTcpClientListener_ForTcpEndpoint() {
    var factory = new TcpClientListenerFactory(AssuanServerOptions.Default);
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    var listener = factory.CreateListener(endpoint);

    listener.ShouldBeOfType<TcpClientListener>();
    listener.Endpoint.ShouldBe(endpoint);
  }

  [Fact]
  public void CreateListener_ShouldThrowNotSupportedException_ForMismatchedEndpoint() {
    var factory = new TcpClientListenerFactory(AssuanServerOptions.Default);
    var endpoint = new NamedPipeEndpoint(".", "unused");

    var ex = Should.Throw<NotSupportedException>(() => factory.CreateListener(endpoint));
    ex.Message.ShouldContain(typeof(NamedPipeEndpoint).FullName!);
  }

  [Fact]
  public void CreateListener_ShouldInvokeConfigureListenerCallback() {
    var invoked = false;
    var options = new AssuanServerOptions { ConfigureListener = _ => invoked = true };
    var factory = new TcpClientListenerFactory(options);
    var endpoint = new TcpClientEndpoint(IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    factory.CreateListener(endpoint);

    invoked.ShouldBeTrue();
  }
}
