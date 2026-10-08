// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Reflection;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace AssuanLibrary.Generators.Tests;

public sealed class DependencyInjectionIntegrationTests {
  [Fact]
  public async Task GeneratedDiCreatesStatePerSessionAndFreshOwnedClientsPerCall() {
    var result = GeneratorHarness.Compile("""
                                          using AssuanLibrary.Generated;
                                          using Microsoft.Extensions.DependencyInjection;
                                          public sealed class State { }
                                          public static class Consumer {
                                            public static int Created;
                                            public static void Configure(IServiceCollection services) {
                                              services.AddAssuanServer<State>(_ => { Created++; return new State(); },
                                                AssuanLibrary.Server.ServerOptions.Default,
                                                new AssuanLibrary.Server.DefaultSessionHooks<State>());
                                              services.AddAssuanClientFactory(AssuanLibrary.Client.ClientOptions.Default);
                                            }
                                          }
                                          """, extraReferences: [
      MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
      MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location)
    ]);
    Assert.Empty(result.OutputCompilationErrors);
    using var image = new MemoryStream();
    var emitted = result.OutputCompilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
    var assembly = Assembly.Load(image.ToArray());
    var consumer = assembly.GetType("Consumer")!;
    var services = new ServiceCollection();
    consumer.GetMethod("Configure")!.Invoke(null, [services]);
    await using var provider = services.BuildServiceProvider();
    var stateType = assembly.GetType("State")!;
    var serverType = typeof(AssuanServer<>).MakeGenericType(stateType);
    var server = provider.GetRequiredService(serverType);
    Assert.Equal(0, consumer.GetField("Created")!.GetValue(null));
    var factoryType = assembly.GetType("AssuanLibrary.Generated.AssuanDependencyInjectionExtensions+AssuanClientFactory")!;
    var factory = provider.GetRequiredService(factoryType);
    var clients = new List<IAsyncDisposable>();
    for (var index = 0; index < 2; ++index) {
      var stream = new MemoryStream("OK\n"u8.ToArray());
      var opening = (Task)factoryType.GetMethod("FromStreamAsync")!.Invoke(factory,
        [stream, TestContext.Current.CancellationToken])!;
      await opening;
      clients.Add(Assert.IsAssignableFrom<IAsyncDisposable>(opening.GetType().GetProperty("Result")!.GetValue(opening)));
    }

    Assert.NotSame(clients[0], clients[1]);
    foreach (var client in clients) {
      await client.DisposeAsync();
    }

    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    using var stop = new CancellationTokenSource();
    var serving = (Task)serverType.GetMethod("ServeAsync")!.Invoke(server, [listener, stop.Token])!;
    using var peer = new TcpClient(AddressFamily.InterNetwork);
    var token = TestContext.Current.CancellationToken;
    await peer.ConnectAsync(listener.Endpoint.Address, listener.Endpoint.Port, token);
    var line = new byte[3];
    await peer.GetStream().ReadExactlyAsync(line, token);
    Assert.Equal("OK\n"u8.ToArray(), line);
    await peer.GetStream().WriteAsync("BYE\n"u8.ToArray(), token);
    await peer.GetStream().ReadExactlyAsync(line, token);
    await stop.CancelAsync();
    await serving.WaitAsync(TimeSpan.FromSeconds(2), token);
    Assert.Equal(1, consumer.GetField("Created")!.GetValue(null));
  }
}
