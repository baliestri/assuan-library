# Assuan protocol primitives

`AssuanLibrary.Protocol` provides binary-safe, I/O-independent building blocks for the Assuan wire format. Parse and encode one line at a time, assemble complete lines under a fixed limit, and choose explicitly how to handle secret bytes. Client and server sessions will use these primitives in later stages; this layer does not connect to a peer or perform authentication.

## Parse a wire line

Pass a line **without** LF or CRLF. Parsing borrows its bytes and does not enforce the 1,000-byte limit; framing enforces that limit. Command arguments preserve the exact bytes after the first separating space, including extra spaces, percent signs and invalid UTF-8:

```csharp
using AssuanLibrary.Protocol;

ReadOnlySpan<byte> line = "GETINFO  version"u8;
var command = ProtocolCommand.Parse(line);
Console.WriteLine(command.NameBytes.Length); // 7
Console.WriteLine(command.Arguments.Length); // 8: leading space + version

var response = ProtocolResponse.Parse("ERR 4294967295 denied"u8);
Console.WriteLine(response.Kind);      // Err
Console.WriteLine(response.ErrorCode); // complete unsigned code
```

`ProtocolResponse.Parse` recognizes `OK`, `ERR`, `D`, `S`, `INQUIRE`, `END`, comments and empty lines. Keyword fields are validated ASCII, while status/inquiry arguments and error text remain binary. A `D ` response retains its **percent-encoded** payload until the caller explicitly decodes it. Calling a category-specific property on another response kind throws `InvalidOperationException`. Keep input buffers stable during the lifetime of a parsed `ref struct` view; a view cannot cross an `await`.

## Encode and decode data

Caller-owned output is a `Span<byte>` of at least 1,000 bytes. A complete command or response encoder validates the full line **before** writing; it includes LF in the returned byte count. Command arguments and `ProtocolResponse.Data` are already in wire form and are not escaped again. For raw binary data, use `EncodeDataChunk` repeatedly to emit `D ` lines without splitting a `%XX` escape:

```csharp
using AssuanLibrary.Protocol;

byte[] output = new byte[ProtocolLineBuffer.MaxLineBytes];
var name = "GETINFO"u8;
var args = "version"u8;
int written = ProtocolEncoder.EncodeCommand(ProtocolCommand.Create(name, args), output);
Console.WriteLine(written); // includes LF

byte[] raw = [(byte)'%', (byte)'\n', 0, 0xff];
var (consumed, bytesWritten) = ProtocolEncoder.EncodeDataChunk(raw, output);
Console.WriteLine($"{consumed} input bytes in {bytesWritten} wire bytes");

byte[] escaped = "%25%0A%00"u8.ToArray();
int decoded = ProtocolEscaping.DecodeDataInPlace(escaped);
// Only escaped.AsSpan(0, decoded) holds the decoded result.
```

The encoder escapes `%`, CR, LF and NUL with uppercase hexadecimal digits. All other data bytes are copied unchanged. The decoder accepts either hex case and checks **every** escape before writing any decoded byte; on `InvalidEscape`, the entire input is unchanged. On success, bytes after the returned decoded prefix can retain old content and must be wiped by their owner when sensitive. Errors expose `ProtocolException.Kind` without including wire content in messages.

## Incremental framing

`ProtocolLineBuffer` owns a zero-initialized allocation of 1,000 bytes. `Feed` consumes input through **at most the first LF** and returns how many bytes the caller must remove; any following bytes belong to the caller. Both LF and CRLF count toward the wire limit. `Line` and `MutableLine` exclude LF and a preceding CR; `IsReady` distinguishes a complete empty line from no line. `FinishEof()` rejects partial input but accepts a complete line awaiting consumption. A framing overflow wipes storage and remains an error until `Clear()`; the connection owner must close an out-of-sync stream, not just clear the local frame.

```csharp
using AssuanLibrary.Protocol;

using var frame = new ProtocolLineBuffer();
ReadOnlySpan<byte> received = "OK\r\nNEXT\n"u8;
int consumed = frame.Feed(received); // 4
Console.WriteLine(System.Text.Encoding.ASCII.GetString(frame.Line)); // OK
frame.Clear(); // wipes all 1,000 bytes
frame.Feed(received[consumed..]); // NEXT
```

Line views and mutable slices become invalid after `Clear()` or `Dispose()`. Diagnostics reveal only categories and metadata, not buffered content. The caller owns read-ahead and scratch buffers outside this frame.

## Fixed-capacity secrets

`SecretBuffer` allocates its entire capacity before inserting bytes. Appends never grow the allocation, and an over-capacity append leaves existing bytes unchanged. `Clear()` wipes **the entire capacity**, resets length and retains the allocation for reuse. `Dispose()` wipes the allocation and prevents later use; use `using` for deterministic cleanup.

```csharp
using AssuanLibrary.Protocol;

using var secret = new SecretBuffer(16);
secret.Append(new byte[] { 0, 0xff });
ReadOnlySpan<byte> exposed = secret.Expose(); // explicit, borrowed access
Console.WriteLine(exposed.Length);
secret.Clear();
```

Protect and clear the source and any caller-created copies separately. Managed cleanup does not wipe previous GC copies, OS buffers, stack temporaries, swap or crash dumps. `PayloadSensitivity` selects public or secret handling in later client/server operations; the enum itself owns no bytes and provides no authentication. TCP authentication, transport deadlines, cancellation and protocol session sequencing are responsibilities of higher layers; the internal state machines validate order but do not perform I/O.
