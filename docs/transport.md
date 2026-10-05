# TCP and custom byte transports

`AssuanLibrary.Transport` connects and accepts **duplex byte streams**. `AssuanChannel` adds bounded, asynchronous line I/O over an already connected `Stream`. This layer does not interpret `OK`/`ERR`, implement an Assuan session, authenticate the peer or add TLS. Unix domain sockets, Windows named pipes and GnuPG discovery have separate implementation stages.

## Connect and accept over TCP

Choose a bind address explicitly. Port zero is useful for a self-contained listener: after binding, `Endpoint` reports the OS-assigned port. A destination with port zero or an unspecified address is rejected. TCP, **including loopback**, does not supply OS-authenticated user identity; `AcceptedConnection.PeerIdentity` is null.

```csharp
using AssuanLibrary.Transport;
using AssuanLibrary.Protocol;
using System.Net;

using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await using var listener = TcpAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
var accepting = listener.AcceptAsync(stop.Token);
using var client = await TcpConnector.ConnectAsync(listener.Endpoint, cancellationToken: stop.Token);
var accepted = await accepting;
using var server = accepted.Stream; // The receiver owns the accepted stream.

byte[] output = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
int written = ProtocolEncoder.EncodeCommand(ProtocolCommand.Create("NOP"u8, ReadOnlySpan<byte>.Empty), output);
await client.WriteAsync(output.AsMemory(0, written), stop.Token);
byte[] received = new byte[written];
await server.ReadExactlyAsync(received, stop.Token);
Console.WriteLine($"Transferred {received.Length} bytes");
```

`TcpConnector.ConnectAsync` uses a ten-second total deadline by default; a zero timeout fails without opening a connection. The connector does not read an Assuan greeting or send a Windows agent nonce. When agent discovery is implemented, use the resolved endpoint's own connection operation for that handshake.

## Framed lines and read-ahead

`AssuanChannel` takes ownership of the supplied `Stream` and allocates fixed storage for the current line, read-ahead and outgoing wire data before receiving bytes. `ReadLineOrEofAsync` returns true for a complete line, false only for EOF at a line boundary; partial EOF throws. `ReadLineAsync` requires a line and throws for any EOF. `Line` omits LF and a preceding CR; `MutableLine` permits explicit in-place decoding. Both views are invalid after the next read or close. A session must release any borrowed event before reading again.

```csharp
using AssuanLibrary.Transport;

var byteStream = new MemoryStream("D hello%0A\nOK\n"u8.ToArray());
await using var channel = new AssuanChannel(byteStream); // Owns the stream from now on.
await channel.ReadLineAsync(TimeSpan.FromSeconds(1));
Console.WriteLine(channel.Line.Length);
Console.WriteLine(channel.HasBufferedInput); // true: OK was read ahead.
await channel.ReadLineAsync(TimeSpan.FromSeconds(1));
Console.WriteLine(channel.Line.Length); // 2 (OK)
```

`WriteLineAsync` accepts exactly one **already encoded** line, including its final LF. It rejects lines longer than 1,000 bytes or containing embedded NUL, CR or LF (one CR immediately before LF is allowed) before writing. One deadline covers the full `Stream.WriteAsync` call **and** flush. .NET `Stream.WriteAsync` does not report a partial byte count: a conforming stream completes the entire requested write or fails. A failure may still follow delivery of a prefix; the channel never retries it.

All three internal buffers are wiped after use or on terminal failure regardless of the caller's sensitivity classification. Read-ahead can contain secret bytes before anyone classifies them. Closing is immediate, discards unread bytes and does not perform a protocol shutdown or implicit flush. The caller still owns its source buffers and any OS buffers. `TransportException.Kind` identifies timeout, cancellation, I/O, framing, closed channel and invalid options/endpoint without exposing payload or external I/O messages; `IoCause` is available for deliberately redacted diagnostics. `ProtocolCause` identifies a framing failure when present.

## Custom streams and acceptors

Any owned `Stream` with ordered asynchronous byte reads/writes can be passed directly to `AssuanChannel`. A custom listener implements `IAssuanAcceptor` and returns `AcceptedConnection`; no fake TCP address is necessary:

```csharp
using AssuanLibrary.Transport;

public sealed class OneConnectionAcceptor(Stream stream) : IAssuanAcceptor {
  private Stream? _stream = stream;

  public ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    var owned = Interlocked.Exchange(ref _stream, null)
      ?? throw new InvalidOperationException("No pending connection.");
    return ValueTask.FromResult(new AcceptedConnection(owned));
  }

  public ValueTask DisposeAsync() {
    Interlocked.Exchange(ref _stream, null)?.Dispose();
    return ValueTask.CompletedTask;
  }
}
```

The application defines authentication and encryption for its custom backend. It must also implement the normal `Stream` contract for full writes, short reads, flush, disposal and cancellation. An I/O operation that ignores cancellation may still hold the channel's buffer after a deadline: the channel closes and forbids reuse, but retains that buffer until the underlying operation completes, then wipes it. It cannot safely claim synchronous erasure of memory still writable by an uncooperative stream. Avoid concurrent operations on one channel; it serializes them rather than sharing internal buffers across active I/O.
