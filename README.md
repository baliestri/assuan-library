# AssuanLibrary

**Build Assuan clients and servers in .NET 10 without treating protocol data as text.** AssuanLibrary provides bounded,
binary-safe protocol handling, asynchronous transactions and pluggable transports for applications that speak the
Assuan protocol, including integrations with an existing GnuPG agent.

> [!NOTE]
> Version 2.0 replaces the published 1.x API. Code and examples written for 1.x are not source-compatible; start from
> the [getting started](wiki/Getting-Started.md) page instead of adapting a 1.x snippet by name.

## What is included

- **Client:** serial transactions with streaming response events, interactive inquiries, greeting callbacks and opt-in
  bounded response collection.
- **Server:** serial sessions with typed handlers, per-connection state and hooks, bounded concurrent acceptance and
  supervised shutdown.
- **Transports:** TCP, Unix domain sockets, Windows named pipes and custom `Stream` objects, plus discovery of and
  connection to an existing GnuPG agent.
- **Protocol:** bounded wire parsing and encoding, line framing, explicit secret storage and canonical binary
  S-expressions.
- **Generator:** an incremental source generator, shipped inside the same package, for handler registration and
  `OPTION` routing. Dependency-injection and logging helpers are generated only when the consumer references the
  corresponding Microsoft.Extensions packages.

## Requirements

- .NET 10. The library targets `net10.0`.
- Windows, Linux or macOS. Named pipes are Windows-only; Unix domain sockets are Linux and macOS only.
- For GnuPG agent discovery: an existing agent and a trusted `gpgconf` executable given by its full path.

## Install

```shell
dotnet add package AssuanLibrary --version 2.1.0
```

The one package contains the runtime and the generator; there is no second package to install.

## A taste

With an Assuan server that implements `ECHO` listening on loopback port 9000, such as the `tcp-server` example below, a
client sends one command and reads its decoded data:

```csharp
using System;
using System.Net;
using AssuanLibrary.Client;

await using var client = await AssuanClient.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 9000));
await using var transaction = await client.CommandAsync("ECHO", "hello"u8.ToArray());
while (await transaction.ReadNextAsync() is { } response) {
  using (response) {
    if (response.Kind == ClientEventKind.Data)
      Console.WriteLine(Convert.ToHexString(response.PayloadBytes.Span)); // 68656C6C6F
  }
}
transaction.Finish();
```

One connection handles one transaction at a time. Dispose each event before reading the next, and call `Finish()` after
the final response. TCP does not authenticate the peer.

## Examples

One console application hosts every example. The first runs a server and a client over in-memory streams, with no
external service:

```shell
dotnet run --project examples/AssuanLibrary.Examples -- --list
dotnet run --project examples/AssuanLibrary.Examples -- --example custom-transport
dotnet run --project examples/AssuanLibrary.Examples -- --example sexpressions
```

For a loopback TCP exchange, run these in two terminals with the same port. The client verifies that `ECHO sample`
returns `sample`; stop the server with Ctrl+C:

```shell
dotnet run --project examples/AssuanLibrary.Examples -- --example tcp-server --port 9000
dotnet run --project examples/AssuanLibrary.Examples -- --example tcp-client --port 9000
```

To query an existing GnuPG agent, pass the full path of a trusted `gpgconf`. Add `--homedir <path>` deliberately; without
it, `gpgconf` uses its configured default. The example neither starts an agent nor displays secrets:

```shell
dotnet run --project examples/AssuanLibrary.Examples -- --example agent-info --gpgconf <full-path>
```

## Documentation

The [wiki](wiki/Home.md) describes how each part behaves, with complete programs.

| To learn about                                   | Read                                                                                              |
| :----------------------------------------------- | :------------------------------------------------------------------------------------------------ |
| Installing and sending a first command           | [Getting started](wiki/Getting-Started.md)                                                        |
| Commands, streaming responses and inquiries      | [Client transactions](wiki/Client-Transactions.md), [client inquiries](wiki/Client-Inquiries.md)  |
| Handlers, hooks and serving many connections     | [Server sessions](wiki/Server-Sessions.md), [concurrent server](wiki/Concurrent-Server.md)        |
| Generated registration and optional integrations | [Generators](wiki/Generators.md), [integrations](wiki/Integrations.md)                            |
| Wire format and binary values                    | [Protocol](wiki/Protocol.md), [S-expressions](wiki/S-Expressions.md)                              |
| TCP, sockets, pipes and the GnuPG agent          | [Transport](wiki/Transport.md), [local IPC](wiki/Local-IPC.md), [agent discovery](wiki/Agent-Discovery.md) |
| What is protected and what is not                | [Security](wiki/Security.md), [troubleshooting](wiki/Troubleshooting.md)                          |

## Verify a release

Releases published after 2.0.1 carry a signed build-provenance attestation for the package. With the GitHub CLI:

```shell
gh attestation verify AssuanLibrary.<version>.nupkg --repo baliestri/assuan-library
```

The same `.nupkg` is attached to the GitHub Release of each such version. Report a suspected vulnerability as described
in the [security policy](SECURITY.md).

## Build from source

```shell
dotnet restore AssuanLibrary.slnx --locked-mode
dotnet build AssuanLibrary.slnx -c Release --no-restore
dotnet test --solution AssuanLibrary.slnx -c Release --no-restore
```

[CONTRIBUTING](CONTRIBUTING.md) covers the project layout, the checks a change must pass and how releases are made.

## License

AssuanLibrary is licensed under the zlib/libpng License; see [LICENSE.md](LICENSE.md).
