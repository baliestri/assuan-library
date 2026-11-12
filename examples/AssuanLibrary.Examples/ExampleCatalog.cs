// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.Client;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Examples;

/// <summary>One no-I/O catalog of selectable demonstrations.</summary>
public static class ExampleCatalog {
  private readonly static IExample[] Entries = [
    new CustomTransportExample(),
    new TcpServerExample(),
    new TcpClientExample(),
    new AgentInfoExample(),
    new SExpressionExample()
  ];

  /// <summary>Dispatches a named example or an interactive menu; returns a process exit code.</summary>
  public static async Task<int> RunAsync(string[] args, TextReader input, TextWriter output,
    TextWriter error, CancellationToken token) {
    ArgumentNullException.ThrowIfNull(args);
    ArgumentNullException.ThrowIfNull(input);
    ArgumentNullException.ThrowIfNull(output);
    ArgumentNullException.ThrowIfNull(error);
    if (token.IsCancellationRequested) {
      return 130;
    }

    switch (args) {
      case ["--list"]:
      {
        foreach (var item in Entries)
          await output.WriteLineAsync($"{item.Name} - {item.Description}").ConfigureAwait(false);

        return 0;
      }
      case ["--help"]:
        PrintHelp(output);
        return 0;
    }

    string name;
    string[] options;

    switch (args.Length) {
      case 0:
      {
        if (await SelectFromMenuAsync(input, output, token).ConfigureAwait(false) is not { } selected) {
          PrintError(error, "Invalid menu selection.");
          return 2;
        }

        name = selected;
        options = [];
        break;
      }
      case >= 2 when string.Equals(args[0], "--example", StringComparison.Ordinal):
        name = args[1];
        options = args[2..];
        break;
      default:
        PrintError(error, "Expected --example <name>.");
        return 2;
    }

    var example = Array.Find(Entries, entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
    if (example is null) {
      PrintError(error, "Unknown example name.");
      return 2;
    }

    return await RunGuardedAsync(example, options, output, error, token).ConfigureAwait(false);
  }

  private static async Task<string?> SelectFromMenuAsync(TextReader input, TextWriter output,
    CancellationToken token) {
    for (var index = 0; index < Entries.Length; ++index)
      await output.WriteLineAsync($"{index + 1}. {Entries[index].Name} - {Entries[index].Description}")
        .ConfigureAwait(false);

    await output.WriteAsync("Select an example: ").ConfigureAwait(false);
    var selection = await input.ReadLineAsync(token).ConfigureAwait(false);
    return int.TryParse(selection, NumberStyles.None, CultureInfo.InvariantCulture, out var chosen) &&
           chosen >= 1 &&
           chosen <= Entries.Length
      ? Entries[chosen - 1].Name
      : null;
  }

  private static async Task<int> RunGuardedAsync(IExample example, string[] options, TextWriter output,
    TextWriter error, CancellationToken token) {
    try { return await example.RunAsync(options, output, error, token).ConfigureAwait(false); }
    catch (OperationCanceledException) {
      await error.WriteLineAsync("Cancelled.").ConfigureAwait(false);
      return 130;
    }
    catch (ClientException cause) {
      await error.WriteLineAsync($"Client error: {cause.Kind}.").ConfigureAwait(false);
      return 1;
    }
    catch (DiscoveryException cause) {
      await error.WriteLineAsync($"Discovery error: {cause.Kind}.").ConfigureAwait(false);
      return 1;
    }
    catch (TransportException cause) {
      await error.WriteLineAsync($"Transport error: {cause.Kind}.").ConfigureAwait(false);
      return 1;
    }
    catch (PlatformNotSupportedException) {
      await error.WriteLineAsync("Unavailable on this platform.").ConfigureAwait(false);
      return 1;
    }
    catch (Exception) {
      await error.WriteLineAsync("Example failed.").ConfigureAwait(false);
      return 1;
    }
  }

  private static void PrintError(TextWriter error, string message) {
    error.WriteLine(message);
    PrintHelp(error);
  }

  private static void PrintHelp(TextWriter output) {
    output.WriteLine("Usage: --list | --example <name> [options]");
    output.WriteLine("Without arguments, select a numbered menu item.");
  }
}
