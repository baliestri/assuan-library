// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Examples;

internal sealed class AgentInfoExample : IExample {
  public string Name => "agent-info";
  public string Description => "Query an existing GnuPG agent selected by gpgconf";

  public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    string? executable = null, homedir = null;
    for (var index = 0; index < args.Length; index += 2) {
      if (index + 1 == args.Length ||
          string.IsNullOrWhiteSpace(args[index + 1])) {
        await error.WriteLineAsync("Specify --gpgconf <path> and optionally --homedir <path>.");
        return 2;
      }

      switch (args[index]) {
        case "--gpgconf" when executable is null:
          executable = args[index + 1];
          break;
        case "--homedir" when homedir is null:
          homedir = args[index + 1];
          break;
        default:
          await error.WriteLineAsync("Specify --gpgconf <path> and optionally --homedir <path>.");
          return 2;
      }
    }

    if (executable is null) {
      await error.WriteLineAsync("Specify --gpgconf <path> and optionally --homedir <path>.");
      return 2;
    }

    if (homedir is null) {
      await output.WriteLineAsync("agent-info: gpgconf will use its default homedir.");
    }

    using var resolved = await new AgentLocator(executable, homedir).ResolveAsync(token);
    await using var client = await AssuanClient.ConnectAsync(resolved, cancellationToken: token);
    await using var tx = await client.CommandAsync("GETINFO", "version"u8.ToArray(), PayloadSensitivity.Public, token);
    var count = 0;
    while (await tx.ReadNextAsync(token) is { } item) {
      using (item) {
        if (item.Kind == ClientEventKind.Data) {
          count += item.PayloadBytes.Length;
        }
      }
    }

    tx.Finish();
    await output.WriteLineAsync($"agent-info: version-bytes={count}");
    return 0;
  }
}
