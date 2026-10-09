// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Client;
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
    var response = await client.CollectAsync("GETINFO", "version"u8.ToArray(),
      new CollectLimits(64, 8), token);
    var version = response.Data;
    if (version.Length is 0 or > 64 ||
        version[0] is < (byte)'0' or > (byte)'9' ||
        version.Any(value => !char.IsAsciiLetterOrDigit((char)value) &&
                             (char)value is not ('.' or '-' or '+' or '_'))) {
      throw new InvalidOperationException("The agent returned an invalid version.");
    }

    await output.WriteLineAsync($"agent-info: GnuPG agent version {Encoding.ASCII.GetString(version)}");
    return 0;
  }
}
