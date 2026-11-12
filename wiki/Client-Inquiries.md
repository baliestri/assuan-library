# Interactive Assuan client inquiries

`INQUIRE` is an exclusive pause in one command or greeting. Its `ClientInquiry` copies the validated keyword and literal argument bytes into fixed protected storage before the receive channel is reused for output. Finish with **`END`** or cancel with **`CAN`**; neither completes the surrounding command or greeting. Read the subsequent final `OK`/`ERR` before reusing a command client or finishing a handshake. `CANCEL` is an ordinary command name, not the wire response `CAN`.

## Answer an inquiry during a command

```csharp
using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

static class CommandInquiryExample {
  public static async Task RunAsync(AssuanClient client, CancellationToken token) {
    await using var transaction = await client.CommandAsync("ASK", ReadOnlyMemory<byte>.Empty,
      PayloadSensitivity.Secret, token);
    ClientEventLease? item;
    while ((item = await transaction.ReadNextAsync(token)) is not null) {
      using (item) {
        if (item.Kind != ClientEventKind.Inquire) continue;
        ClientInquiry inquiry = item.Inquiry;
        Console.WriteLine($"Keyword length: {inquiry.KeywordBytes.Length}");
        // Secret arguments require inquiry.ExposeSecretArguments().
        using var answer = new SecretBuffer(4);
        answer.Append(new byte[] { 0, 10, 37, 255 });
        await inquiry.SendSecretAsync(answer, token); // Caller still owns answer.
        await inquiry.FinishAsync(token);             // Sends END, not the command's OK.
      }
    }
    transaction.Finish(); // A remote ERR is typed and does not by itself ruin the connection.
  }
}
```

Use `SendDataAsync(ReadOnlyMemory<byte>)` for public raw bytes. Both send methods percent-escape special bytes in bounded `D ` lines and send one empty `D ` line for empty input. The **1 MiB default** counts raw bytes accumulated across the inquiry; an over-limit call fails before sending any bytes from that call and closes the client. Its deadline is the smaller of 120 seconds and the command's remaining deadline. Do not retain `ReadOnlyMemory<byte>` views after their owner is finished or disposed; C# cannot revoke an escaped alias. Secret metadata and scratch storage are wiped by the library, while caller-owned `SecretBuffer` instances and external copies remain the caller's responsibility.

Discarding an unanswered inquiry or its event lease closes the connection. After a successful `CAN`, release the event lease and continue reading for the final response. A final command `ERR` remains a recoverable remote error; the same final during greeting rejects the connection.

## Manual greeting

`BeginHandshake` takes ownership of an already connected stream **without I/O**. Its original greeting deadline starts at creation and includes any pauses between reads or callbacks. `Finish()` transfers the ready client only after a final `OK` and release of the last event lease.

```csharp
using AssuanLibrary.Client;

static class ManualGreetingExample {
  public static async Task<AssuanClient> ConnectAsync(Stream connected, CancellationToken token) {
    await using var handshake = AssuanClient.BeginHandshake(connected);
    ClientEventLease? item;
    while ((item = await handshake.ReadNextAsync(token)) is not null) {
      using (item) {
        if (item.Kind == ClientEventKind.Inquire)
          await item.Inquiry.CancelAsync(token); // Still wait for the final greeting line.
      }
    }
    return handshake.Finish();
  }
}
```

`ClientOptions` validates positive, representable deadlines at construction, before a handshake can begin. Premature `Finish()` or disposal closes the owned stream. A full unsigned greeting `ERR` code is available as `ClientException.RemoteCode`, without its remote text appearing in exception diagnostics.

## Greeting callback or safe default

Implement `IGreetingInquiryHandler.RespondAsync` to answer each greeting inquiry and call `FinishAsync` or `CancelAsync` before returning. `AssuanClient.FromStreamAsync(connected, handler, options, token)` and the endpoint `ConnectAsync` overloads accept this callback. The resolved-agent overload still performs its native Windows nonce handshake first.

```csharp
using AssuanLibrary.Client;

sealed class CancelGreetingInquiry : IGreetingInquiryHandler {
  public ValueTask RespondAsync(ClientInquiry inquiry, CancellationToken token)
    => new(inquiry.CancelAsync(token));
}
```

Without a callback, `FromStreamAsync` and the endpoint connection overloads automatically send `CAN` for each greeting inquiry **and keep reading** until a final `OK` or `ERR`; `CAN` alone is never acceptance. A callback that returns without finishing its inquiry makes the connection unusable. If a callback ignores cancellation, the channel closes on timeout while the callback's borrowed metadata remains owned until its task actually finishes; .NET cannot forcibly interrupt arbitrary application code. Bounded `CollectAsync` and `CollectSecretAsync` also count inquiry events, automatically send `CAN`, and wait for the required final response.
