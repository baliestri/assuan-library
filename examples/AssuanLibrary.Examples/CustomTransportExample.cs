// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;

namespace AssuanLibrary.Examples;

internal sealed class CustomTransportExample : IExample {
  public string Name => "custom-transport";
  public string Description => "Exchange a public binary response between an in-memory server and client";

  public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    if (args.Length != 0) {
      await error.WriteLineAsync("custom-transport takes no arguments.").ConfigureAwait(false);
      return 2;
    }

    var (clientSide, serverSide) = MemoryDuplexPair.Create();
    var acceptor = new SingleConnectionAcceptor(serverSide);
    await using (acceptor.ConfigureAwait(false)) {
      using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
      var host = new AssuanServer<State>(() => new State(), new ServerOptions(maxSessions: 1));
      host.Register(new BinaryHandler());
      var serving = host.ServeAsync(acceptor, stop.Token);
      try {
        var client = await AssuanClient.FromStreamAsync(clientSide, cancellationToken: token).ConfigureAwait(false);
        await using (client.ConfigureAwait(false)) {
          var response = await client.CollectAsync("BINARY", ReadOnlyMemory<byte>.Empty,
            new CollectLimits(16, 4), token).ConfigureAwait(false);
          await output.WriteLineAsync($"custom-transport: BINARY reply=0x{Convert.ToHexString(response.Data)}")
            .ConfigureAwait(false);

          var bye = await client.CommandAsync("BYE", ReadOnlyMemory<byte>.Empty,
            PayloadSensitivity.Public, token).ConfigureAwait(false);
          await using (bye.ConfigureAwait(false)) {
            using (AssertFinal(await bye.ReadNextAsync(token).ConfigureAwait(false))) { }

            bye.Finish();
            await output.WriteLineAsync("custom-transport: BYE completed").ConfigureAwait(false);
            return 0;
          }
        }
      }
      finally {
        await stop.CancelAsync().ConfigureAwait(false);
        await serving.ConfigureAwait(false);
        await host.WhenDrainedAsync(CancellationToken.None).ConfigureAwait(false);
      }
    }
  }

  private static ClientEventLease AssertFinal(ClientEventLease? response)
    => response is not { Kind: ClientEventKind.Finished }
      ? throw new InvalidOperationException("Missing final response.")
      : response;

  private sealed class State;

  private sealed class BinaryHandler : ICommandHandler<State> {
    public string Name => "BINARY";
    public string Description => "Send two binary bytes";

    public ValueTask HandleAsync(CommandContext<State> context, CancellationToken token)
      => context.SendDataAsync(new byte[] { 0, 255 }, token);
  }
}
