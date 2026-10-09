// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Client;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Examples;

internal sealed class TcpClientExample : IExample {
  public string Name => "tcp-client";
  public string Description => "Send ECHO to an explicit IPv4 loopback server";

  public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    if (!ExampleArguments.TryPort(args, error, out var port)) {
      return 2;
    }

    await using var client = await AssuanClient.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port),
      cancellationToken: token);
    var request = "sample"u8.ToArray();
    var reply = new byte[request.Length];
    var received = 0;
    await using (var tx = await client.CommandAsync("ECHO", request, PayloadSensitivity.Public, token)) {
      while (await tx.ReadNextAsync(token) is { } item) {
        using (item) {
          if (item.Kind != ClientEventKind.Data) {
            continue;
          }

          if (item.PayloadBytes.Length > (reply.Length - received)) {
            throw new InvalidOperationException("ECHO returned more data than requested.");
          }

          item.PayloadBytes.Span.CopyTo(reply.AsSpan(received));
          received += item.PayloadBytes.Length;
        }
      }

      tx.Finish();
    }

    if (received != request.Length ||
        !reply.AsSpan().SequenceEqual(request)) {
      throw new InvalidOperationException("ECHO response differed from the request.");
    }

    await using (var bye = await client.CommandAsync("BYE", ReadOnlyMemory<byte>.Empty,
                   PayloadSensitivity.Public, token)) {
      using (var final = await bye.ReadNextAsync(token)) {
        if (final is not { Kind: ClientEventKind.Finished }) {
          throw new InvalidOperationException("Missing final response.");
        }
      }

      bye.Finish();
    }

    await output.WriteLineAsync("tcp-client: ECHO sample -> sample (verified)");
    return 0;
  }
}
