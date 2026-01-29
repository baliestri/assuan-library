// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Client;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server;
using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Sample;

internal static class CustomServer {
  private static readonly TcpClientEndpoint endpoint = new(IPAddress.Loopback, 8080);

  public static async Task RunAsync(CancellationToken ct = default) {
    var server = new AssuanServer();
    server.RegisterCommandHandler<GetInfoCommandHandler>();

    await server.RunAsync(endpoint, ct);
  }

  public static async Task RunClientAsync() {
    await using var client = new AssuanClient();
    await client.ConnectAsync(endpoint, new Dictionary<string, object>());

    var command = new AssuanCommand("GETINFO") { "version" };
    var responseCollection = await client.InvokeAsync(command);
    foreach (var response in responseCollection) {
      Console.WriteLine($"\t→ {response:GT}");
    }
  }

  private sealed class GetInfoCommandHandler : CommandHandler {
    /// <inheritdoc />
    public override string Name => "GETINFO";

    /// <inheritdoc />
    public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
      foreach (var arg in command.Arguments) {
        Console.WriteLine($"DEBUG → GETINFO argument: '{arg}'");

        switch (arg) {
          case "version": {
            var responseCollection = AssuanResponseCollection.Create(
              AssuanResponse.Data(typeof(GetInfoCommandHandler).Assembly.GetName().Version?.ToString() ?? "unknown"),
              AssuanResponse.Ok()
            );
            await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
            break;
          }
          case "pid": {
            var responseCollection = AssuanResponseCollection.Create(
              AssuanResponse.Data(Environment.ProcessId.ToString()),
              AssuanResponse.Ok()
            );
            await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
            break;
          }
          default:
            await serverContext.SendResponseAsync(AssuanResponse.Error(1, $"Unknown GETINFO argument: {arg}"),
              serverContext.Session.CancellationToken);
            break;
        }
      }
    }
  }
}
