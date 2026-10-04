# Canonical S-expressions

`AssuanLibrary.SExpressions` parses and writes **canonical binary** S-expressions. It is the first implemented layer of the 2.0 rewrite. An atom is encoded as its decimal byte length, a colon, then the exact bytes. Lists surround consecutive expressions with `(` and `)`. No whitespace is skipped outside atom payloads.

## Parse without copying atom bytes

```csharp
using AssuanLibrary.SExpressions;

byte[] input = "(3:foo0:)"u8.ToArray();
var expression = SExpressionParser.ParseComplete(input);

foreach (var child in expression.Children) {
  Console.WriteLine($"Atom length: {child.AtomBytes.Length}"); // 3, then 0
}
```

`SExpressionView` is a scoped `ref struct`. Parsed atoms point directly into the caller's buffer; list iteration does not construct an index tree or copy payloads. Keep `input` alive and **do not change or reuse it while using the view**. A view cannot be retained in a normal field or across an `await`. Atom bytes can be non-UTF-8, so there is no implicit text conversion. An empty list has no children; trying to read `AtomBytes` from a list (or `Children` from an atom) throws `InvalidOperationException`.

Use `ParseComplete` when the entire input must be one expression. `ParsePrefix` reads only the first expression, reports its byte count, and leaves subsequent expressions to the caller:

```csharp
using AssuanLibrary.SExpressions;

ReadOnlySpan<byte> remaining = "(1:a)1:b"u8;
var first = SExpressionParser.ParsePrefix(remaining, out int consumed);
Console.WriteLine(consumed); // 5
remaining = remaining[consumed..];
var second = SExpressionParser.ParseComplete(remaining);
Console.WriteLine(second.AtomBytes.Length); // 1
```

The input-byte limit applies to **all** bytes supplied to `ParsePrefix`, including the suffix not parsed by that call. Neither parser trims spaces or line endings.

## Keep a value beyond its input

`ToOwned()` explicitly copies every atom into an immutable `SExpression` tree. Its `Atom` factory also copies the source bytes, and `List` copies the child collection. The value can be stored or carried across awaits:

```csharp
using AssuanLibrary.SExpressions;

var retained = SExpressionParser.ParseComplete("(3:foo0:)"u8).ToOwned();
await Task.Yield();
Console.WriteLine(retained.Children.Count); // 2

var built = SExpression.List([
  SExpression.Atom("foo"u8),
  SExpression.Atom(new byte[] { 0, 0xff, (byte)')' }),
]);
```

The owned representation uses ordinary managed memory. It does **not** promise to wipe secret data. `ToString()` and parser/encoder exceptions show structure, lengths, categories or byte offsets without including atom contents; this does not make the backing storage itself secret-safe.

## Write canonical bytes

```csharp
using AssuanLibrary.SExpressions;

var built = SExpression.List([SExpression.Atom("foo"u8)]);
byte[] canonical = SExpressionWriter.ToCanonical(built);
var output = new List<byte>("prefix:"u8.ToArray());
SExpressionWriter.AppendCanonical(built, output);
```

Byte lengths are based on the payload bytes (for example, UTF-8 `é` has length **2**). The writer uses the **default** limits even for manually constructed trees or a view originally parsed with larger custom limits. Appending includes the destination's existing length in the output limit. Limit failures occur before appending, leaving existing output bytes and count unchanged. The output is an ordinary byte array/list and is not automatically erased.

## Limits and error handling

| Resource | Default | Configurable behavior |
| --- | ---: | --- |
| Entire input / encoded output | 16 MiB | Input may be set to any positive `int`; output always uses defaults |
| Atom payload | 16 MiB | Can be zero, cannot exceed maximum input |
| Atom and list nodes combined | 65,536 | Must be positive |
| Nested lists | 64 | Zero permits only a root atom; maximum supported is 256 |

```csharp
using AssuanLibrary.SExpressions;

var limits = new SExpressionLimits(maxInputBytes: 1024, maxAtomBytes: 256, maxNodes: 100, maxDepth: 8);
try {
  SExpressionParser.ParseComplete("01:a"u8, limits);
} catch (SExpressionParseException error) {
  Console.WriteLine($"{error.Kind} at byte {error.Offset}"); // InvalidLength at byte 1
}
```

`SExpressionParseException.Kind` identifies invalid syntax, incomplete input, overflow or exceeded limits; `Offset` is a zero-based byte index. Invalid limit configuration has no input offset. Writer errors expose `SExpressionEncodeException.Kind`. Neither exception includes the input or payload bytes in its message. Runtime exhaustion such as a catastrophic `OutOfMemoryException` cannot be promised as a recoverable domain error.
