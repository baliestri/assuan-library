# Automatic optional DI and Logging integrations

The **single** `AssuanLibrary` NuGet package contains the runtime assembly and its analyzer. The analyzer checks types available in the *consumer compilation*: it generates DI helpers when `Microsoft.Extensions.DependencyInjection` supplies `IServiceCollection`, `AddSingleton` and the concrete `ServiceProvider`; it generates Logging helpers when `ILogger` and `ILoggerFactory` are available. Neither library is a mandatory dependency of `AssuanLibrary`. No marker attribute, separate extensions package or manual analyzer reference is needed by an installed-package consumer.

| Consumer references | Generated integration |
| --- | --- |
| Neither Microsoft library | None; handler/group/OPTION generation remains available |
| `Microsoft.Extensions.DependencyInjection` | `AssuanDependencyInjectionExtensions` |
| `Microsoft.Extensions.Logging.Abstractions` only | `AssuanLoggingExtensions` |
| Both | Both, each generated once |

The Logging abstractions may bring DI *abstractions* transitively; that alone does not enable DI generation. For local **project references** during development, add the generator as an analyzer reference as shown in `tests/AssuanLibrary.Tests/AssuanLibrary.Tests.csproj`. Installation through the main NuGet package carries it automatically at `analyzers/dotnet/cs/`.

## DI with explicit ownership

Add the DI package to your application and import `AssuanLibrary.Generated` for the extension methods:

```csharp
using AssuanLibrary.Client;
using AssuanLibrary.Generated;
using AssuanLibrary.Server;
using Microsoft.Extensions.DependencyInjection;

static class IntegrationExample {
  public static async Task ConfigureAsync<TState>(Func<IServiceProvider, TState> stateFactory,
    ISessionHooks<TState> hooks, ICommandHandler<TState> handler) {
    var services = new ServiceCollection();
    services.AddAssuanServer(stateFactory, ServerOptions.Default, hooks, handler);
    services.AddAssuanClientFactory(ClientOptions.Default);
    await using var provider = services.BuildServiceProvider();
    var server = provider.GetRequiredService<AssuanServer<TState>>();
    var clients = provider.GetRequiredService<AssuanDependencyInjectionExtensions.AssuanClientFactory>();
    Console.WriteLine($"Pending sessions: {server.PendingSessionCount}; factory: {clients.GetType().Name}");
    // Each clients.ConnectAsync(endpoint, token) / FromStreamAsync(stream, token) returns
    // a new AssuanClient owned by its caller. Neither connects during container construction.
  }
}
```

Supply your own state factory, hooks and handler as in the [server guide](server.md) and [generator guide](generators.md). The factory runs **once per admitted connection**, not when building the provider. It does not create a DI scope automatically or manage scoped state dependencies; a consumer that needs per-connection scope must own it explicitly. The helper does not bind an endpoint, start the server, authenticate TCP, discover handlers automatically or retry client transactions. The server is `IAsyncDisposable`: asynchronously dispose the provider after serving, while supervising any pending session work.

## Logging without sensitive payloads

With `Microsoft.Extensions.Logging.Abstractions` referenced, `AssuanLoggingExtensions.CreateEventSink(ILogger)` adapts a logger to the package-independent `IAssuanEventSink`:

```csharp
using AssuanLibrary.Client;
using AssuanLibrary.Diagnostics;
using AssuanLibrary.Generated;
using AssuanLibrary.Server;
using Microsoft.Extensions.Logging;

static class LoggingExample {
  public static (ServerOptions Server, ClientOptions Client) Configure(ILogger logger) {
    IAssuanEventSink sink = AssuanLoggingExtensions.CreateEventSink(logger);
    return (new ServerOptions(eventSink: sink), new ClientOptions(eventSink: sink));
  }
}
```

Events contain only fixed component/ID/outcome enums and numeric counts, not wire data, command text, remote diagnostics, pathnames, nonce, application state or `Exception` objects. The generated logger adapter calls `ILogger.Log` with a **null exception**. A throwing observer is isolated by the runtime event dispatcher and cannot change an Assuan result. With no sink configured, no event is built or sent.

Use `CreateEventSink(ILoggerFactory)` when an explicit logger factory is available. If both integrations are referenced, pass the resulting sink explicitly through `ServerOptions`/`ClientOptions` when registering DI services; DI alone never requires Logging. The analyzer emits fixed source hints and reports `ASG011` on a collision with its fixed generated integration class, rather than duplicating a class or silently renaming methods. Local package tests inspect `lib/net10.0/` and `analyzers/dotnet/cs/` and compile installed consumers in all four configurations. This stage does not publish to a remote feed.
