# Getting started with AssuanLibrary 2.0

AssuanLibrary targets .NET 10. The 2.0 API is a complete break from the published 1.x package. Until 2.0 is published, a local project can reference `src/AssuanLibrary/AssuanLibrary.csproj`; a packaged consumer will use `dotnet add package AssuanLibrary --version 2.0.0`. The main package includes its handler generator as a compiler analyzer; no second generator package is installed.

Run the one console application with `dotnet run --project examples/AssuanLibrary.Examples -- --list` to see `sexpressions`, `custom-transport`, `tcp-server`, `tcp-client` and `agent-info`. `sexpressions` and `custom-transport` require neither GnuPG nor a TCP port. For a two-terminal loopback session, run `--example tcp-server --port 9000`, then `--example tcp-client --port 9000`; stop the server with Ctrl+C. The TCP demonstration has no peer authentication. `agent-info` requires a trusted `--gpgconf <path>` and optionally `--homedir <path>`; without the latter, gpgconf uses its own configured default and the example never starts the agent.

For a small local operation, parse canonical bytes from a `u8` literal without a generator attribute:

```csharp
using System;
using AssuanLibrary.SExpressions;
var value = SExpressionParser.ParseComplete("(3:foo0:)"u8);
foreach (var child in value.Children) Console.WriteLine(child.IsAtom ? child.AtomBytes.Length : 0);
```

For networked work, select an explicit `LocalEndpoint`/`IPEndPoint` and create `AssuanClient.ConnectAsync` or an `AssuanServer<TState>` with a per-session state factory. Read one client transaction event at a time, dispose its lease, then call `Finish()` after the final response. Command bytes and inquiry payloads are not implicitly UTF-8 text. Authentication is an application policy for TCP; a default hook does not provide it.

The source guides under `docs/` describe each layer: `s-expressions.md`, `protocol.md`, `transport.md`, `local-ipc.md`, `agent-discovery.md`, `client-transactions.md`, `client-inquiries.md`, `server-session.md`, `server.md`, `generators.md` and `integrations.md`. Read the security and troubleshooting guides before handling sensitive data or diagnosing a failed connection.
