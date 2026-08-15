// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Platform.TestHelpers;

namespace AssuanLibrary.Tests.Platform.Windows.Transport;

[Collection(nameof(PlatformTransportCollection))]
public sealed class NamedPipeListenerFactoryTests {
  [Fact]
  public void CreateListener_ShouldReturnNamedPipeListener_ForNamedPipeEndpoint() {
    var factory = new NamedPipeListenerFactory(AssuanServerOptions.Default);
    var endpoint = new NamedPipeEndpoint(".", $"assuan-test-{Guid.NewGuid():N}");

    var listener = factory.CreateListener(endpoint);

    listener.ShouldBeOfType<NamedPipeListener>();
    listener.Endpoint.ShouldBe(endpoint);
  }

  [Fact]
  public void CreateListener_ShouldThrowNotSupportedException_ForMismatchedEndpoint() {
    var factory = new NamedPipeListenerFactory(AssuanServerOptions.Default);
    var endpoint = new TcpClientEndpoint(System.Net.IPAddress.Loopback, (ushort)LoopbackHarness.GetFreeTcpPort());

    var ex = Should.Throw<NotSupportedException>(() => factory.CreateListener(endpoint));
    ex.Message.ShouldContain(typeof(TcpClientEndpoint).FullName!);
  }

  [Fact]
  public void CreateListener_ShouldInvokeConfigureListenerCallback() {
    var invoked = false;
    var options = new AssuanServerOptions { ConfigureListener = _ => invoked = true };
    var factory = new NamedPipeListenerFactory(options);
    var endpoint = new NamedPipeEndpoint(".", $"assuan-test-{Guid.NewGuid():N}");

    factory.CreateListener(endpoint);

    invoked.ShouldBeTrue();
  }
}
