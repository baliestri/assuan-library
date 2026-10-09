// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Attributes;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Examples;

internal sealed class TcpExampleState { }

[AssuanHandlerGroup]
internal static partial class TcpExampleHandlers {
  [AssuanCommandHandler("ECHO", "Echo wire arguments")]
  private static ValueTask EchoAsync(CommandContext<TcpExampleState> context, CancellationToken token)
    => context.SendDataAsync(context.Arguments, token);
}

internal sealed class TcpServerExample : IExample {
  public string Name => "tcp-server";
  public string Description => "Serve ECHO on explicit IPv4 loopback until cancellation";

  public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    if (!ExampleArguments.TryPort(args, error, out var port)) {
      return 2;
    }

    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, port));
    var host = new AssuanServer<TcpExampleState>(() => new TcpExampleState(), ServerOptions.Default);
    TcpExampleHandlers.RegisterHandlers(host);
    host.SetHooks(new DefaultSessionHooks<TcpExampleState>()); // Loopback-only demonstration; no TCP peer authentication.
    await output.WriteLineAsync($"tcp-server: ECHO handler ready on 127.0.0.1:{port} (no TCP authentication)");
    await host.ServeAsync(listener, token);
    await output.WriteLineAsync($"tcp-server: pending sessions={host.PendingSessionCount}");
    if (host.PendingSessionCount != 0) {
      using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(2));
      try { await host.WhenDrainedAsync(drain.Token); }
      catch (OperationCanceledException) { await output.WriteLineAsync($"tcp-server: still pending={host.PendingSessionCount}"); }
    }

    return 0;
  }
}
