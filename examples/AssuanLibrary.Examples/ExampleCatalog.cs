// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.Client;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Examples;

internal interface IExample {
  string Name { get; }
  string Description { get; }
  Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token);
}

/// <summary>One no-I/O catalog of selectable demonstrations.</summary>
public static class ExampleCatalog {
  private static readonly IExample[] _entries = [
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
      case ["--list"]: {
        foreach (var item in _entries) {
          await output.WriteLineAsync($"{item.Name} - {item.Description}");
        }

        return 0;
      }
      case ["--help"]:
        PrintHelp(output);
        return 0;
    }

    string name;
    string[] options;

    switch (args.Length) {
      case 0: {
        for (var index = 0; index < _entries.Length; ++index) {
          await output.WriteLineAsync($"{index + 1}. {_entries[index].Name} - {_entries[index].Description}");
        }

        await output.WriteAsync("Select an example: ");
        var selection = await input.ReadLineAsync(token).ConfigureAwait(false);
        if (!int.TryParse(selection, NumberStyles.None,
              CultureInfo.InvariantCulture, out var chosen) ||
            chosen < 1 ||
            chosen > _entries.Length) {
          PrintError(error, "Invalid menu selection.");
          return 2;
        }

        name = _entries[chosen - 1].Name;
        options = [];
        break;
      }
      case >= 2 when
        args[0] == "--example":
        name = args[1];
        options = args[2..];
        break;
      default:
        PrintError(error, "Expected --example <name>.");
        return 2;
    }

    var example = Array.Find(_entries, entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
    if (example is null) {
      PrintError(error, "Unknown example name.");
      return 2;
    }

    try { return await example.RunAsync(options, output, error, token).ConfigureAwait(false); }
    catch (OperationCanceledException) {
      await error.WriteLineAsync("Cancelled.");
      return 130;
    }
    catch (ClientException cause) {
      await error.WriteLineAsync($"Client error: {cause.Kind}.");
      return 1;
    }
    catch (DiscoveryException cause) {
      await error.WriteLineAsync($"Discovery error: {cause.Kind}.");
      return 1;
    }
    catch (TransportException cause) {
      await error.WriteLineAsync($"Transport error: {cause.Kind}.");
      return 1;
    }
    catch (PlatformNotSupportedException) {
      await error.WriteLineAsync("Unavailable on this platform.");
      return 1;
    }
    catch (Exception) {
      await error.WriteLineAsync("Example failed.");
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
