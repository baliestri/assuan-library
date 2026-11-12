# AssuanLibrary wiki

These pages describe how each part of AssuanLibrary behaves today, with complete programs. What the project is and how to install it are in the
[README](../README.md); building, validating and sending changes are in [CONTRIBUTING](../CONTRIBUTING.md).

## Start here

| Page                                  | What it covers                                                                  |
| :------------------------------------ | :------------------------------------------------------------------------------ |
| [Getting started](Getting-Started.md) | Installing the package, the selectable examples and a first bounded command.    |
| [Security](Security.md)               | What the library protects, and what remains the application's responsibility.   |
| [Troubleshooting](Troubleshooting.md) | Greeting failures, the Windows agent nonce, Unix socket cleanup and shutdown.   |

## Client

| Page                                          | What it covers                                                          |
| :-------------------------------------------- | :---------------------------------------------------------------------- |
| [Client transactions](Client-Transactions.md) | Connecting, streaming response events and opt-in bounded collection.    |
| [Client inquiries](Client-Inquiries.md)       | Answering `INQUIRE` with `END` or `CAN`, manual greetings and callbacks. |

## Server

| Page                                      | What it covers                                                             |
| :---------------------------------------- | :------------------------------------------------------------------------- |
| [Server sessions](Server-Sessions.md)     | One accepted stream: handlers, hooks, inquiries and buffer lifetimes.      |
| [Concurrent server](Concurrent-Server.md) | Bounded admission, supervised shutdown and pending sessions.               |
| [Generators](Generators.md)               | Generated handler registration and `OPTION` routing.                       |
| [Integrations](Integrations.md)           | Dependency-injection and logging helpers generated when the consumer references them. |

## Protocol and transports

| Page                                  | What it covers                                                              |
| :------------------------------------ | :-------------------------------------------------------------------------- |
| [Protocol](Protocol.md)               | Wire parsing and encoding, line framing and secret storage.                 |
| [S-expressions](S-Expressions.md)     | Canonical binary S-expressions: parsing, scoped views, owned values, writing. |
| [Transport](Transport.md)             | TCP, custom streams and channel ownership.                                  |
| [Local IPC](Local-IPC.md)             | Unix domain sockets and Windows named pipes, with their access policies.    |
| [Agent discovery](Agent-Discovery.md) | Finding an existing GnuPG agent with `gpgconf`, and the Windows nonce.      |
