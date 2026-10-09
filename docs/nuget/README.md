# AssuanLibrary

**Build Assuan clients and servers in .NET 10 without treating protocol data as text.** AssuanLibrary provides bounded, binary-safe protocol handling, asynchronous transactions, and transport abstractions for applications that speak the Assuan protocol, including integrations with existing GnuPG agents.

## What the package includes

- A serial client API for commands, streaming responses, inquiries, and opt-in bounded response collection.
- Server sessions with typed handlers, per-connection state, hooks, and supervised shutdown.
- TCP, Unix domain socket, Windows named-pipe, and custom `Stream` transports; discovery and connection to an existing GnuPG agent.
- Canonical binary S-expression parsing and writing, plus an incremental handler generator bundled in the same package. DI and Logging helpers are generated only when the consumer references the corresponding Microsoft.Extensions packages.

## Install

```shell
dotnet add package AssuanLibrary --version 2.0.0
```

The package targets **`net10.0`**. Version 2.0 replaces the published 1.x API; 1.x code and examples are not source-compatible.

## Send a command

This example connects to an Assuan TCP server listening on loopback port 9000 that implements `ECHO`. It reads decoded data events without retaining borrowed buffers:

```csharp
using System;
using System.Net;
using AssuanLibrary.Client;

await using var client = await AssuanClient.ConnectAsync(
    new IPEndPoint(IPAddress.Loopback, 9000));
await using var transaction = await client.CommandAsync("ECHO", "hello"u8.ToArray());

while (await transaction.ReadNextAsync() is { } response) {
    using (response) {
        if (response.Kind == ClientEventKind.Data)
            Console.WriteLine(Convert.ToHexString(response.PayloadBytes.Span));
    }
}

transaction.Finish();
```

One client connection handles one transaction at a time. Dispose each event before reading the next; call `Finish()` after the final response. TCP does **not** authenticate the peer—apply an authentication policy for services that need one. When connecting to a discovered Windows GnuPG agent, use the resolved endpoint's connection method so its native nonce is sent before the greeting.

## Learn more

- [Getting started](https://baliestri.github.io/assuan-library/2.0.0/guides/getting-started.html) and [client transactions](https://baliestri.github.io/assuan-library/2.0.0/guides/client-transactions.html)
- [Server sessions](https://baliestri.github.io/assuan-library/2.0.0/guides/server-session.html), [GnuPG agent discovery](https://baliestri.github.io/assuan-library/2.0.0/guides/agent-discovery.html), and [ownership and security](https://baliestri.github.io/assuan-library/2.0.0/guides/security.html)
- [Public API reference](https://baliestri.github.io/assuan-library/2.0.0/api/toc.html) and [selectable client/server examples](https://github.com/baliestri/assuan-library/tree/v2.0.0/examples/AssuanLibrary.Examples)
