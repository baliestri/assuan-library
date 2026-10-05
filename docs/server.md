# Bounded concurrent Assuan server

`AssuanServer<TState>` owns one `IAssuanAcceptor` and supervises independent serial `ServerSession<TState>` instances. Configure shared handlers and hooks before calling `ServeAsync` once. A new state is created for each accepted connection; handlers can run at the same time in **different** sessions. The default capacity is 128, including authentication and close hooks. The host reserves capacity **before** accepting another connection, so the transport owns its backlog.

## Complete loopback host

This example uses TCP loopback for local development. The default hooks do **not** authenticate TCP clients; install an application authentication hook before exchanging secrets. See [the session guide](server-session.md) for the hook contract and inquiry lifetimes.

```csharp
using System.Net;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

sealed class ConnectionState { public int Commands; }

sealed class Echo : ICommandHandler<ConnectionState> {
  public string Name => "ECHO";
  public string Description => "Echo the supplied wire arguments";

  public async ValueTask HandleAsync(CommandContext<ConnectionState> context, CancellationToken token) {
    context.State.Commands++;
    await context.SendDataAsync(context.Arguments, token);
  }
}

static class ServerExample {
  public static async Task RunAsync(CancellationToken stop) {
    await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    var host = new AssuanServer<ConnectionState>(() => new ConnectionState(),
      new ServerOptions(maxSessions: 8));
    host.Register(new Echo());
    // Configure application authentication with host.SetHooks(...) for real clients.
    try {
      await host.ServeAsync(listener, stop); // Transfers ownership of listener to the host.
    }
    catch (ServerException error) when (error.Kind == ServerErrorKind.Transport) {
      // A failure to accept a connection ended admission; examine TechnicalCause
      // separately and redact it before recording any OS-supplied details.
      throw;
    }

    if (host.PendingSessionCount != 0) {
      using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      try { await host.WhenDrainedAsync(drain.Token); }
      catch (OperationCanceledException) {
        // The caller stopped waiting; the host still supervises those sessions.
      }
    }
  }
}
```

Pass a shutdown token that the application cancels when it wants to stop accepting. `ServeAsync` disposes the acceptor, then allows **one shared 30-second grace period** for existing sessions to finish, including `BYE`. If a handler ignores cancellation past the grace, the host requests its stop and closes its channel, but retains its state, buffers and capacity permit until it actually finishes. `ServeAsync` can return while `PendingSessionCount` is nonzero; `WhenDrainedAsync` waits for true completion, and cancelling that wait does not discard a session. Call `DisposeAsync` to request stop; it does not forcibly terminate arbitrary application code.

Session or state-factory failures affect only that connection. An acceptor failure ends admission and is returned as a redacted `ServerException` with `Kind == Transport`; its `TechnicalCause` is deliberately separate from safe diagnostics. The host emits only failure **categories** through the `AssuanLibrary.Server` diagnostic listener (`SessionFailure`). It never logs command arguments, application state, peer data or external exception messages.

Unix listeners require a private, current-user-owned directory. Disposing a `UnixAcceptor` via the host **does not unlink its pathname**; the caller retains the listener reference and must call `Cleanup()` explicitly after verifying that cleanup is appropriate. Named pipe and TCP listeners close on disposal without that pathname step. This host does not implement client transactions, generated handlers or dependency injection.
