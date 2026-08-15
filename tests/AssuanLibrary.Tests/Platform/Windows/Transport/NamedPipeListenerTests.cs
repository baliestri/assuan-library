// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;
using System.Runtime.Versioning;
using AssuanLibrary.Platform.Windows.Transport;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Tests.Platform.TestHelpers;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Platform.Windows.Transport;

[Collection(nameof(PlatformTransportCollection))]
[SupportedOSPlatform("windows")]
public sealed class NamedPipeListenerTests {
  [Fact]
  public void Endpoint_ShouldReturnConfiguredEndpoint() {
    var endpoint = new NamedPipeEndpoint(".", $"assuan-test-{Guid.NewGuid():N}");
    var listener = new NamedPipeListener(endpoint, AssuanListenerOptions.Default);

    listener.Endpoint.ShouldBe(endpoint);
  }

  [Fact(Skip = "Real named-pipe I/O hangs the test host on this dev machine - see " +
    "StabilizedNamedPipeReaderTests.NAMED_PIPE_HANG_SKIP_REASON for details.")]
  public async Task Accept_ShouldReturnConnectedConnection_WhenClientConnects() {
    var name = $"assuan-test-{Guid.NewGuid():N}";
    var endpoint = new NamedPipeEndpoint(".", name);
    var listener = new NamedPipeListener(endpoint, AssuanListenerOptions.Default);

    var acceptTask = Task.Run(() => listener.Accept());

    await Task.Delay(50);
    using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.None);
    await client.ConnectAsync(2000);
    client.ReadMode = PipeTransmissionMode.Message;

    var connection = await acceptTask;
    await using var _ = connection;

    connection.IsConnected.ShouldBeTrue();

    client.Write("hello"u8);
    var result = connection.Read();
    result.ShouldBe("hello"u8.ToArray());
  }
}
