# One Assuan server session

`ServerSession<TState>` handles one **already accepted** byte stream. It owns that stream, performs an authentication hook before the greeting, executes commands serially, and emits the single final `OK` or `ERR` after each command. Concurrent acceptance and shutdown coordination belong to the later server-host stage.

## Register handlers and state

Create a registry before running the session. The first session freezes the registry; subsequent registration fails. Names are case-sensitive ASCII tokens; `NOP`, `BYE`, `HELP`, `RESET` and `OPTION` are reserved. Each accepted connection should receive its own application state.

```csharp
using AssuanLibrary.Server;
using AssuanLibrary.Transport;
using AssuanLibrary.Protocol;

sealed class SessionState { public int Requests; }

sealed class Echo : ICommandHandler<SessionState> {
  public string Name => "ECHO";
  public string Description => "Return the supplied wire arguments";

  public async ValueTask HandleAsync(CommandContext<SessionState> context, CancellationToken token) {
    context.State.Requests++;
    await context.SendDataAsync(context.Arguments, token);
    // The runner sends OK after the handler returns.
  }
}

sealed class RequireVerifiedPeer : ISessionHooks<SessionState> {
  public ValueTask AuthenticateAsync(CommandContext<SessionState> context, CancellationToken token) {
    if (context.PeerIdentity is null)
      return ValueTask.FromException(HandlerException.Remote(55, "authentication required"));
    return ValueTask.CompletedTask;
  }
  public ValueTask OptionAsync(OptionRequest request, CommandContext<SessionState> context,
    CancellationToken token) => ValueTask.FromException(HandlerException.Remote(174, "unknown option"));
  public ValueTask ResetAsync(CommandContext<SessionState> context, CancellationToken token)
    => ValueTask.CompletedTask;
  public ValueTask ClosedAsync(SessionState state, SessionEnd reason, CancellationToken token)
    => ValueTask.CompletedTask;
}

static class SessionExample {
  // Use an acceptor that supplies verified PeerIdentity for this policy.
  // A custom/TCP stream needs application authentication instead.
  public static async Task RunAsync(AcceptedConnection accepted, CancellationToken cancellationToken) {
    var handlers = new CommandRegistry<SessionState>();
    handlers.Register(new Echo());
    var session = new ServerSession<SessionState>(accepted, new SessionState(), handlers,
      new RequireVerifiedPeer(), ServerOptions.Default);
    await session.RunAsync(cancellationToken);
  }
}
```

The default hooks accept a stream already admitted by its transport, without authenticating a TCP peer. Install application authentication when transport identity is absent or insufficient. `PeerIdentity` is non-null only when a transport has verified it. The `IsAuthenticated` flag becomes true after your hook succeeds and remains true across `RESET`.

## Hooks and errors

Implement `ISessionHooks<TState>` for `AuthenticateAsync` (before greeting), `OptionAsync`, `ResetAsync` and `ClosedAsync`. An authentication hook can send status or perform an inquiry through its context. `OPTION` preserves literal value bytes and distinguishes an absent value from an explicitly empty one. Reset only the transient application state in `ResetAsync`; the library keeps its authentication flag. The close hook receives a payload-free `SessionEnd` reason after the active handler has finished.

Throw `HandlerException.Remote(code, publicText)` for a peer-visible, validated one-line `ERR`; use `HandlerException.Internal()` for a redacted `ERR 63 internal error`. The runner sends exactly one final response. I/O failures, invalid protocol input and unfinished inquiries close the connection without inventing a final response. Exception diagnostics omit command arguments, remote text and underlying I/O messages; a remote message is **public**, so never put secrets in it.

## Inquiry and buffer lifetimes

`BeginInquiryAsync(keyword, arguments, PayloadSensitivity.Secret, token)` sends `INQUIRE` and yields an exclusive `ServerInquiry`. Read one `InquiryChunk` at a time, explicitly call `Expose()` for secret bytes, dispose the chunk, and repeat until `ReadNextAsync` returns null. Then call `Finish()` to distinguish `END` from `CAN`. Empty `D ` lines yield empty chunks. Abandoning an unfinished inquiry closes the session, even when a handler catches its own exception.

Inside a handler with `context` and `token` parameters:

```csharp
await using var inquiry = await context.BeginInquiryAsync(
  "VALUE", ReadOnlyMemory<byte>.Empty, PayloadSensitivity.Secret, token);
InquiryChunk? chunk;
while ((chunk = await inquiry.ReadNextAsync(token)) is not null) {
  using (chunk) {
    ReadOnlyMemory<byte> borrowed = chunk.Expose();
    // Consume borrowed bytes here; do not keep them after Dispose.
  }
}
InquiryOutcome outcome = inquiry.Finish();
```

The channel and command arguments are **borrowed** buffers: `ReadOnlyMemory<byte>` stays stable across awaits in the current invocation, but aliases become invalid after the handler or chunk completes. Copy only data that must outlive it and protect sensitive copies separately. The inquiry counts decoded bytes against a 1 MiB default total, not a per-line limit.

Authentication and greeting share 120 seconds by default, a command plus final response 300 seconds, an inquiry 120 seconds capped by its command, idle time 300 seconds and a close hook 30 seconds. All deadlines are total budgets; data fragments and comments do not restart them. Cancellation is cooperative: an unresponsive handler can keep `RunAsync` pending, while the channel closes and its buffers and state remain owned until it actually returns. Use cancellation tokens in handlers for predictable shutdown.
