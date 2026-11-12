// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using System.Net;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Examples;

internal sealed class TcpServerExample : IExample {
  public string Name => "tcp-server";
  public string Description => "Serve ECHO on explicit IPv4 loopback until cancellation";

  public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    if (!ExampleArguments.TryPort(args, error, out var port)) {
      return 2;
    }

    var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, port));
    await using (listener.ConfigureAwait(false)) {
      var host = new AssuanServer<TcpExampleState>(() => new TcpExampleState(), ServerOptions.Default);
      TcpExampleHandlers.RegisterHandlers(host);
      host.SetHooks(
        new DefaultSessionHooks<TcpExampleState>()); // Loopback-only demonstration; no TCP peer authentication.
      await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
          $"tcp-server: ECHO handler ready on 127.0.0.1:{port} (no TCP authentication)"))
        .ConfigureAwait(false);
      await host.ServeAsync(listener, token).ConfigureAwait(false);
      await output
        .WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
          $"tcp-server: pending sessions={host.PendingSessionCount}")).ConfigureAwait(false);
      if (host.PendingSessionCount != 0) {
        using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await host.WhenDrainedAsync(drain.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) {
          await output
            .WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
              $"tcp-server: still pending={host.PendingSessionCount}")).ConfigureAwait(false);
        }
      }

      return 0;
    }
  }
}
