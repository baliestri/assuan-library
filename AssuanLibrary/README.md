# Assuan Library (.NET)

A small, high-performance .NET library for interacting with the Assuan protocol (used by GnuPG components such as gpg-agent and pinentry).

This repository contains the implementation and a sample program that demonstrates communicating with an Assuan-compatible agent over a socket.

## Highlights

* Lightweight, allocation-friendly encoder/decoder helpers: `AssuanEncoder`, `AssuanDecoder`.
* Structured command representation: `AssuanCommand`.
* Typed responses: `AssuanResponse`, `AssuanResponseCollection`, `AssuanResponseType`.
* Async, disposable client wrapper in `AssuanClient` with an "inquire" callback model for interactive requests.
* Small helper types and parsers for symbolic expressions used by some Assuan data responses.

## Compatibility

* [x] Windows
* [x] Linux
* [ ] macOS (untested — contributions welcome)

## Installation

Install the NuGet package:

```shell
dotnet add package AssuanLibrary
```

Or add a project/package reference directly in your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="AssuanLibrary" Version="1.0.0" />
</ItemGroup>
```

## Quickstart

The following examples show the most common operations: creating commands, encoding/decoding, and invoking commands using the client.

### Encode / Decode helpers

The library exposes convenience methods for encoding user input into the Assuan wire format and decoding percent-encoded responses.

```csharp
using AssuanLibrary;

var encoded = AssuanEncoder.AsString("GETINFO version");
var bytes = AssuanEncoder.AsBytes("SOME DATA");

var decoded = AssuanDecoder.ToString(encoded);
var decodedBytes = AssuanDecoder.ToBytes(bytes);
```

### Building commands

Use `AssuanCommand` to construct or inspect commands sent to the agent.
Arguments are added with `Add` or via collection initializers.

```csharp
using AssuanLibrary;

var cmd = new AssuanCommand("GETINFO") { "version" };
Console.WriteLine(cmd.ToString()); // Encoded command string
```

### Invoking commands with `AssuanClient`

`AssuanClient` is an async, disposable client that connects to an Assuan socket and performs command invocation.
The sample program demonstrates common usage and interactive inquire handling.

Below is a compact example that connects, requests the agent version, and prints the responses:

```csharp
using AssuanLibrary;
using AssuanLibrary.Network;

// Create options pointing to the agent socket (use SocketDescriptor.AgentSocket for default agent socket).
var options = new AssuanClientOptions(SocketDescriptor.AgentSocket);

await using var client = new AssuanClient(options);
await client.ConnectAsync();

var versionCmd = new AssuanCommand("GETINFO") { "version" };
var responses = await client.InvokeAsync(versionCmd);

foreach (var r in responses) {
  Console.WriteLine($"{r.Type}: {r}");
}
```

Some commands (like `GET_PASSPHRASE`, `GET_CONFIRMATION`) require the client to handle "inquire" callbacks.
The sample demonstrates handling the `IInquireContext` callback where you can respond to keywords (e.g., `PASSPHRASE`, `QUALITY`, `CONFIRM`).

## Building and running the sample

This repository contains a small sample program demonstrating typical usage.
From the repository root, build and run with dotnet:

```shell
dotnet build
dotnet run --project AssuanLibrary.Sample/AssuanLibrary.Sample.csproj
```

Note: replace any placeholder keygrips or values in the sample before running real signing operations.

## Contributing

Contributions are welcome.
A good starting point is:

* Open an issue to discuss changes or bug reports.
* Fork the repository, implement your change, and send a pull request.
* Keep changes small and focused, add tests for new behavior, and run the existing test suite.

Please follow the existing code style and naming conventions used in the project.

## License

This project is licensed under the MIT License — see the `LICENSE.md` file for details.
