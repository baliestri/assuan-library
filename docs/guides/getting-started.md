# Getting started with AssuanLibrary 2.0

AssuanLibrary targets .NET 10. The 2.0 API is a complete break from the published 1.x package. Until 2.0 is published, a local project can reference `src/AssuanLibrary/AssuanLibrary.csproj`; a packaged consumer will use `dotnet add package AssuanLibrary --version 2.0.0`. The main package includes its handler generator as a compiler analyzer; no second generator package is installed.

Run the one console application with `dotnet run --project examples/AssuanLibrary.Examples -- --list` to see `custom-transport`, `tcp-server`, `tcp-client`, `agent-info` and `sexpressions`. The default first example, `custom-transport`, starts an in-memory Assuan server and client and displays the fixed public binary reply `0x00FF`; `sexpressions` is a separate parser demonstration. Neither requires GnuPG nor a TCP port. For a two-terminal loopback session, run `--example tcp-server --port 9000`, then `--example tcp-client --port 9000`; the client verifies that `ECHO sample` returns `sample`. Stop the server with Ctrl+C. This TCP demonstration has no peer authentication. `agent-info` requires a trusted `--gpgconf <path>` and optionally `--homedir <path>`; without the latter, gpgconf uses its own configured default and the example never starts the agent.

With the example TCP server running on loopback port 9000, a client can collect a small public response with an explicit bound:

```csharp
using System;
using System.Net;
using AssuanLibrary.Client;

await using var client = await AssuanClient.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 9000));
var reply = await client.CollectAsync("ECHO", "hello"u8.ToArray(),
  new CollectLimits(maxBytes: 64, maxEvents: 8));
Console.WriteLine(Convert.ToHexString(reply.Data)); // 68656C6C6F
```

`CollectAsync` makes an explicitly bounded copy of public response data. For streaming instead, read one client transaction event at a time, dispose its lease, then call `Finish()` after the final response. Server applications use an `AssuanServer<TState>` with a per-session state factory; clients can connect to an explicit `LocalEndpoint`/`IPEndPoint` or a discovered agent endpoint. Command bytes and inquiry payloads are not implicitly UTF-8 text. Authentication is an application policy for TCP; a default hook does not provide it.

The source guides under `docs/` describe each layer: `client-transactions.md`, `server-session.md`, `protocol.md`, `transport.md`, `local-ipc.md`, `agent-discovery.md`, `client-inquiries.md`, `server.md`, `generators.md`, `integrations.md` and `s-expressions.md`. Read the security and troubleshooting guides before handling sensitive data or diagnosing a failed connection.
