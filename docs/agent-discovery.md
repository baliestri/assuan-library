# Discover an existing GnuPG agent

`AgentLocator` queries an **explicitly selected** `gpgconf` executable with `--list-dirs agent-socket`. It neither starts `gpg-agent` nor reads the Assuan greeting. Pass an optional homedir deliberately; without one, `gpgconf` uses its own configured default. Choose a trusted executable and configuration directory before exchanging secrets.

```csharp
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;

string gpgconf = args[0]; // Supply a trusted executable path explicitly.
string? homeDirectory = args.Length > 1 ? args[1] : null;
using var locatorResult = await new AgentLocator(gpgconf, homeDirectory).ResolveAsync();
var connected = await locatorResult.ConnectAsync();
await using var channel = new AssuanChannel(connected); // Channel owns the connected stream.
await channel.ReadLineAsync(TimeSpan.FromSeconds(5));
Console.WriteLine($"Greeting length: {channel.Line.Length}");
```

The locator supplies `--homedir <path>` as separate process arguments when requested; it never uses a shell. The named `gpgconf` output is an **unescaped literal pathname**: `%20` denotes the characters percent, two and zero, not a space. A five-second total deadline covers starting the process, draining both output pipes, waiting for exit, and reading the native socket file on Windows. Stdout and stderr have one **combined 1 MiB** budget, with both drained concurrently. Errors, timeouts and cancellations terminate and wait for the selected process when possible. Captured output, full paths and nonce bytes are omitted from exception messages and `ToString()`.

## Unix sockets

Linux and macOS discovery preserve the original pathname **bytes**, including names that are not valid UTF-8. `ResolvedAgentEndpoint.UnixPathBytes` exposes them explicitly; `ConnectAsync` uses a lossless managed path for valid UTF-8 and an isolated byte-path connector otherwise. It does not percent-decode or replace unrecognized characters. The socket's parent directory belongs to the external service: the current-user-private parent policy applies only when this library **creates a listener**, not when connecting to an existing agent.

```csharp
using AssuanLibrary.Transport.Discovery;

string gpgconf = args[0];
string? homeDirectory = args.Length > 1 ? args[1] : null;
using var resolved = await new AgentLocator(gpgconf, homeDirectory).ResolveAsync();
if (resolved.Kind == AgentEndpointKind.UnixSocket) {
  Console.WriteLine($"Unix pathname bytes: {resolved.UnixPathBytes.Length}");
}
using var stream = await resolved.ConnectAsync(); // Does not read the greeting.
```

Raw Unix pathname bytes are never implicitly converted to text in diagnostics. Unix non-UTF-8 behavior is exercised with an independent byte-named socket in Linux tests; macOS still requires its own hosted validation.

## Native Windows agent socket

The pathname returned by `gpgconf` on Windows denotes a **native socket file**, not a named pipe. That file must be a regular file with a decimal TCP port from 1 to 65535, one LF byte and **exactly 16 binary nonce bytes**; a valid file is at most 22 bytes. Discovery reads at most 23 to detect excess. Signs, CRLF, trailing bytes and the Cygwin `!<socket >` format are rejected. The nonce is retained privately in fixed-capacity `SecretBuffer` storage and wiped when the resolved endpoint is disposed.

`ResolvedAgentEndpoint.TcpEndpoint` reveals only the loopback port. Connecting to it directly **omits the nonce handshake**. Use `ResolvedAgentEndpoint.ConnectAsync`: it connects to IPv4 `127.0.0.1`, writes all 16 nonce bytes, flushes and only then returns the owned stream. Connection, write and flush share a ten-second default deadline; the greeting remains unread. A failed or cancelled attempt closes its stream without retrying partially delivered credentials. On platforms where an OS file read cannot be interrupted immediately, its temporary buffer remains owned until that read completes and is then wiped.

## Errors and verification scope

`DiscoveryException.Kind` distinguishes invalid/process output, missing executable/I/O, unsupported socket format, aggregate output limit, timeout and cancellation. The separately available `IoCause` may contain OS-supplied detail: apply your own redaction before logging it. Disposing the resolved endpoint prevents further connections and clears its owned nonce; caller-owned buffers, OS buffers and copies made by the runtime require separate handling.

The deterministic tests use a controlled `gpgconf` fixture and a literal TCP/Unix peer, with their files confined to repository-local ignored artifacts. They verify the documented formats and handshake without requiring GnuPG installed. A real-agent interoperability test is a separate release gate; fixture success alone is not evidence that every installed GnuPG version is compatible.
