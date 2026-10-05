# Streaming Assuan client transactions

`AssuanClient` owns a connected stream and reads the Assuan greeting before returning, cancelling greeting inquiries with `CAN` by default. It accepts an already-connected `Stream`, an explicit TCP/Unix/named-pipe endpoint, or a `ResolvedAgentEndpoint`. The last form calls the resolved endpoint's own `ConnectAsync`, including the **native Windows nonce handshake**; a bare `TcpEndpoint` cannot replace it. No command is sent on connection.

```csharp
using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

static class ClientExample {
  public static async Task RunAsync(Stream connected, CancellationToken token) {
    await using var client = await AssuanClient.FromStreamAsync(connected,
      ClientOptions.Default, token); // Takes ownership of connected.
    await using var transaction = await client.CommandAsync("GETINFO", "version"u8.ToArray(),
      PayloadSensitivity.Public, token); // Arguments are already wire bytes.

    ClientEventLease? item;
    while ((item = await transaction.ReadNextAsync(token)) is not null) {
      using (item) {
        if (item.Kind == ClientEventKind.Data)
          Console.WriteLine($"Data bytes: {item.PayloadBytes.Length}");
        if (item.Kind == ClientEventKind.Status)
          Console.WriteLine($"Status keyword bytes: {item.KeywordBytes.Length}");
      }
    }
    transaction.Finish(); // Does not read or drain more bytes.
  }
}
```

Only one transaction may use a client at a time; use another connection for concurrency. `D` data is percent-decoded **in place**, while status arguments and comments remain literal wire bytes. `END` is a partial response: only final `OK`/`ERR` ends a command. An `ERR` final raises a typed `ClientException` with the complete unsigned `RemoteCode` from `Finish()`; a completed command may then be followed by another. An unfinished transaction, invalid response, timeout or cancelled I/O closes the client. `IsUsable` describes local state, not whether the peer remains alive.

Event content aliases a protected channel buffer. Dispose each `ClientEventLease` before reading again or calling `Finish()`. A `ReadOnlyMemory<byte>` saved from an event becomes **invalid for use** after that lease is disposed; C# cannot revoke the alias. For a secret-classified command, use `ExposeSecret()` instead of `PayloadBytes`. The category and optional error code remain available without exposing content. No event content or remote diagnostic text appears in exception messages or `ToString()`.

## Explicit bounded collection

Streaming keeps response data borrowed. Request retention only when necessary:

```csharp
using AssuanLibrary.Client;

static class CollectionExample {
  public static async Task RunAsync(AssuanClient client, CancellationToken token) {
    var limits = new CollectLimits(maxBytes: 64 * 1024, maxEvents: 100);
    CollectedResponse publicResult = await client.CollectAsync("GETINFO", "version"u8.ToArray(),
      limits, token);
    Console.WriteLine(publicResult.Data.Length);

    using SecretCollectedResponse secretResult = await client.CollectSecretAsync("GET",
      ReadOnlyMemory<byte>.Empty, limits, token);
    Console.WriteLine(secretResult.ExposeData().Length);
  }
}
```

Collection defaults are 16 MiB of retained bytes and 4,096 observed events. Every event counts, including `END` and the final response; retained status keywords, their separator, arguments and comments count toward the byte budget. Secret collection holds **only decoded data** in a fixed-capacity `SecretBuffer`, wiped on disposal. Status and comments remain in ordinary managed memory. If those metadata could be sensitive, use secret-classified **streaming** instead. Caller-owned command arguments, copies and OS/runtime buffers remain the caller's responsibility.

Connection, greeting and command budgets default to 10, 120 and 300 seconds. A command's single budget includes pauses between event reads, not just socket I/O. Cancellation or uncertain I/O invalidates the client rather than retrying. Interactive `INQUIRE` is handled through an exclusive event guard; see [the inquiry guide](client-inquiries.md) for command replies, manual greeting and callback/default `CAN` policies.
