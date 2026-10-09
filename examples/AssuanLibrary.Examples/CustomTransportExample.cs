// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;

namespace AssuanLibrary.Examples;

internal sealed class CustomTransportExample : IExample {
  public string Name => "custom-transport";
  public string Description => "Run a server and client over an in-memory duplex stream";

  public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    if (args.Length != 0) {
      await error.WriteLineAsync("custom-transport takes no arguments.");
      return 2;
    }

    var (clientSide, serverSide) = MemoryDuplexPair.Create();
    await using var acceptor = new SingleConnectionAcceptor(serverSide);
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
    var host = new AssuanServer<State>(() => new State(), new ServerOptions(maxSessions: 1));
    host.Register(new BinaryHandler());
    var serving = host.ServeAsync(acceptor, stop.Token);
    try {
      await using var client = await AssuanClient.FromStreamAsync(clientSide, cancellationToken: token);
      await using (var transaction = await client.CommandAsync("BINARY", ReadOnlyMemory<byte>.Empty,
                     PayloadSensitivity.Secret, token)) {
        var received = 0;
        while (await transaction.ReadNextAsync(token) is { } item) {
          using (item) {
            if (item.Kind == ClientEventKind.Data) {
              received += item.ExposeSecret().Length;
            }
          }
        }

        transaction.Finish();
        await output.WriteLineAsync($"custom-transport: decoded-bytes={received}");
      }

      await using var bye = await client.CommandAsync("BYE", ReadOnlyMemory<byte>.Empty,
        PayloadSensitivity.Public, token);
      using (var final = AssertFinal(await bye.ReadNextAsync(token))) { }

      bye.Finish();
      return 0;
    }
    finally {
      await stop.CancelAsync();
      await serving.ConfigureAwait(false);
      await host.WhenDrainedAsync(CancellationToken.None).ConfigureAwait(false);
    }
  }

  private static ClientEventLease AssertFinal(ClientEventLease? response)
    => response is not { Kind: ClientEventKind.Finished } ? throw new InvalidOperationException("Missing final response.") : response;

  private sealed class State { }

  private sealed class BinaryHandler : ICommandHandler<State> {
    public string Name => "BINARY";
    public string Description => "Send two binary bytes";

    public ValueTask HandleAsync(CommandContext<State> context, CancellationToken token)
      => context.SendDataAsync(new byte[] { 0, 255 }, token);
  }
}
