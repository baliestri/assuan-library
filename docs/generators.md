# Incremental server handler generation

`AssuanLibrary.Generators` removes repeated **server registration and OPTION dispatch** code. Public annotations live in `AssuanLibrary.Attributes` in the main library; the generator targets `netstandard2.0`, references Roslyn but not the runtime library. An installed `AssuanLibrary` NuGet package includes it automatically as an analyzer; local project-reference consumers can use an explicit analyzer reference as the solution tests do.

```csharp
using AssuanLibrary.Attributes;
using AssuanLibrary.Server;

public sealed class ConnectionState { public int OptionsHandled; }

[AssuanHandlerGroup]
public static partial class Commands {
  [AssuanCommandHandler("ECHO", "Echoes command arguments")]
  private static ValueTask EchoAsync(CommandContext<ConnectionState> context, CancellationToken token)
    => context.SendDataAsync(context.Arguments, token);

  [AssuanOptionHandler("mode")]
  private static ValueTask SetModeAsync(OptionRequest request,
    CommandContext<ConnectionState> context, CancellationToken token) {
    context.State.OptionsHandled++;
    // request.Value preserves absent versus empty values and borrows wire bytes.
    return ValueTask.CompletedTask;
  }
}

static class Configuration {
  public static void Configure(AssuanServer<ConnectionState> server) {
    Commands.RegisterHandlers(server); // Explicit; never called automatically.

    ISessionHooks<ConnectionState> policy = new RequireVerifiedPeer();
    server.SetHooks(Commands.CreateHooks(policy));
    // Direct ServerSession users can instead call Commands.RegisterHandlers(registry)
    // on their own CommandRegistry<ConnectionState>.
  }
}

sealed class RequireVerifiedPeer : ISessionHooks<ConnectionState> {
  public ValueTask AuthenticateAsync(CommandContext<ConnectionState> context, CancellationToken token)
    => context.PeerIdentity is null
      ? ValueTask.FromException(HandlerException.Remote(55, "authentication required"))
      : ValueTask.CompletedTask;
  public ValueTask OptionAsync(OptionRequest request, CommandContext<ConnectionState> context,
    CancellationToken token) => ValueTask.FromException(HandlerException.Remote(174, "unknown option"));
  public ValueTask ResetAsync(CommandContext<ConnectionState> context, CancellationToken token)
    => ValueTask.CompletedTask;
  public ValueTask ClosedAsync(ConnectionState state, SessionEnd reason, CancellationToken token)
    => ValueTask.CompletedTask;
}
```

`RegisterHandlers` has explicit overloads for `AssuanServer<TState>` and `CommandRegistry<TState>`. `CreateHooks(inner)` requires a caller-supplied hook: the generated wrapper routes exact ASCII option-name bytes to annotated methods and delegates unknown options, authentication, reset and close to `inner` unchanged. Choosing `DefaultSessionHooks<TState>` as `inner` is explicit and **does not authenticate TCP**. No generated adapter sends final `OK`/`ERR`, copies borrowed command/option buffers, uses reflection or starts a connection.

Classes must be top-level `partial` classes, static or nonstatic. Annotated command methods are implemented static methods with signature `(CommandContext<TState>, CancellationToken) -> ValueTask`; option methods use `(OptionRequest, CommandContext<TState>, CancellationToken) -> ValueTask`. All methods in a group must use the same accessible `TState`. `AssuanCommandHandler` also works without a group, exposing the individual `<MethodName>Handler` adapter. Duplicate/reserved command tokens, repeated option names, collisions with generated members, invalid signatures or unsupported classes fail at compilation.

| Diagnostic | Category |
| --- | --- |
| `ASG001` | Unsupported class/container (for example, not partial) |
| `ASG002` | Generated member name conflicts with an existing member |
| `ASG006` | Invalid command method signature, overload or state accessibility |
| `ASG007` | Invalid/reserved command name or HELP description |
| `ASG008` | Empty group, incompatible states or invalid grouped method |
| `ASG009` | Invalid option handler signature or state accessibility |
| `ASG010` | Invalid/duplicate option name or option handler outside a group |

There are **no literal attributes**. For Assuan command or canonical S-expression bytes use normal C# byte literals (`u8`) and the runtime `ProtocolCommand` or `SExpressionParser` APIs. The Rust `command!`/`sexpr!` expression macros do not translate into useful class attributes in C#. The analyzer also generates [optional DI/Logging integration](integrations.md) automatically when the corresponding consumer references are available, without another attribute or a mandatory Microsoft dependency.
