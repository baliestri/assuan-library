// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text;
using AssuanLibrary.Expressions;
using AssuanLibrary.Network;

namespace AssuanLibrary.Sample;

internal static class Program {
  private static async Task Main() {
    const string KEYGRIP = "A1B2C3D4E5F6..."; // ← replace with a real keygrip from your setup
    var options = new AssuanClientOptions(SocketDescriptor.AgentSocket);

    await ShowVersionAndOtherInfoAsync(options);
    await ListSecretKeysAsync(options);
    await GetKeyInfoAsync(KEYGRIP, options);
    await SignChallengeAsync(KEYGRIP, options);
    await GetPassphraseAsync(options);
    await GetPinentryConfirmationAsync(options);
    await CheckKeyAvailabilityAsync(KEYGRIP, options);

    await Task.CompletedTask;
  }

  private static async Task ShowVersionAndOtherInfoAsync(AssuanClientOptions options) {
    await using var client = new AssuanClient(options);
    await client.ConnectAsync();

    var versionCommand = new AssuanCommand("GETINFO") { "version" };
    var versionResponseCollection = await client.InvokeAsync(versionCommand);
    Console.WriteLine("gpg-agent version:");
    foreach (var response in versionResponseCollection) {
      Console.WriteLine($"  → {response}");
    }

    var pidCommand = new AssuanCommand("GETINFO") { "pid" };
    var pidResponseCollection = await client.InvokeAsync(pidCommand);
    Console.WriteLine("Agent PID:");
    foreach (var response in pidResponseCollection) {
      Console.WriteLine($"  → {response}");
    }
  }

  private static async Task ListSecretKeysAsync(AssuanClientOptions options) {
    await using var client = new AssuanClient(options);
    await client.ConnectAsync();

    var cmd = new AssuanCommand("KEYINFO") { "--list" };
    var responseCollection = await client.InvokeAsync(cmd);

    Console.WriteLine("Secret keys available in agent:");
    if (responseCollection.Count == 0) {
      Console.WriteLine("  (none found)");
      return;
    }

    foreach (var response in responseCollection) {
      // Typical format: KEYINFO <keygrip> <type> <serial> <is_cached> ...
      Console.WriteLine($"  → {response}");
    }
  }

  private static async Task GetKeyInfoAsync(string keygrip, AssuanClientOptions options) {
    await using var client = new AssuanClient(options);
    await client.ConnectAsync();

    var cmd = new AssuanCommand("KEYINFO") { keygrip };
    var responseCollection = await client.InvokeAsync(cmd);

    Console.WriteLine($"Key info for {keygrip}:");
    foreach (var response in responseCollection) {
      Console.WriteLine($"  → {response}");
    }
  }

  private static async Task SignChallengeAsync(string keygrip, AssuanClientOptions options) {
    await using var client = new AssuanClient(options);
    await client.ConnectAsync();

    const string VALUE_TO_SIGN = "Hello World";
    var bytesToSign = Encoding.UTF8.GetBytes(VALUE_TO_SIGN);
    var sha256 = SHA256.HashData(bytesToSign);
    var hexValue = Convert.ToHexStringLower(sha256);

    var sigKeyCommand = new AssuanCommand("SIGKEY") { keygrip };
    var sigKeyResponseCollection = await client.InvokeAsync(sigKeyCommand);
    if (sigKeyResponseCollection.Count > 1 ||
        sigKeyResponseCollection.Any(response => response.Type == AssuanResponseType.Error)) {
      Console.WriteLine("SIGKEY command failed:");
      foreach (var response in sigKeyResponseCollection) {
        Console.WriteLine($"  → {response}");
      }

      return;
    }

    var setHashCommand = new AssuanCommand("SETHASH") { "--hash=sha256", hexValue };
    var setHashResponseCollection = await client.InvokeAsync(setHashCommand);
    if (setHashResponseCollection.Count > 1 ||
        setHashResponseCollection.Any(response => response.Type == AssuanResponseType.Error)) {
      Console.WriteLine("SETHASH command failed:");
      foreach (var response in setHashResponseCollection) {
        Console.WriteLine($"  → {response}");
      }

      return;
    }

    var pkSignCommand = new AssuanCommand("PKSIGN");
    var pkSignResponseCollection = await client.InvokeAsync(pkSignCommand);
    if (!pkSignResponseCollection.Any(response => response.Type == AssuanResponseType.Data)) {
      Console.WriteLine("PKSIGN command failed:");
      foreach (var response in pkSignResponseCollection) {
        Console.WriteLine($"  → {response}");
      }

      return;
    }

    var symbolicExpressionResponse = pkSignResponseCollection.FirstOrDefault(response => response.Type == AssuanResponseType.Data);

    if (SymbolicExpressionParser.TryParse(symbolicExpressionResponse, out var expr, out var _)) {
      Console.WriteLine("PKSIGN response (signature):");
      Console.WriteLine(SymbolicExpression.PrettyPrint(expr));
    }
  }

  private static async Task GetPassphraseAsync(AssuanClientOptions options) {
    await using var client = new AssuanClient(options);
    await client.ConnectAsync();

    var cmd = new AssuanCommand("GET_PASSPHRASE") {
      "--data",
      Guid.CreateVersion7().ToString(),
      "x", // NO ERROR MESSAGE
      "Passphrase:",
      "Provide your passphrase for confirmation."
    };

    var responseCollection = await client.InvokeAsync(cmd, async (ctx, ct) => {
      switch (ctx.Keyword) {
        case "PASSPHRASE":
          await ctx.WriteAsync("my-super-secret-passphrase", ct);
          await ctx.EndAsync(ct);
          return;
        case "QUALITY":
          await ctx.WriteAsync("80", ct);
          await ctx.EndAsync(ct);
          return;
        default:
          await ctx.CancelAsync(ct);
          break;
      }
    });
    Console.WriteLine("GET_PASSPHRASE result:");
    foreach (var response in responseCollection) {
      Console.WriteLine($"  → {response.Type} {response}");
    }
  }

  private static async Task GetPinentryConfirmationAsync(AssuanClientOptions options) {
    await using var client = new AssuanClient(options);
    await client.ConnectAsync();

    var cmd = new AssuanCommand("GET_CONFIRMATION") {
      "Test Confirmation"
    };

    var responseCollection = await client.InvokeAsync(cmd, async (ctx, ct) => {
      switch (ctx.Keyword) {
        case "CONFIRM":
          // Don't send any extra data, just end the request to confirm
          await ctx.EndAsync(ct);
          return;
        default:
          await ctx.CancelAsync(ct);
          break;
      }
    });

    Console.WriteLine("GET_CONFIRMATION result:");
    foreach (var response in responseCollection) {
      Console.WriteLine($"  → {response.Type} {response}");
    }
  }

  private static async Task CheckKeyAvailabilityAsync(string keygrip, AssuanClientOptions options) {
    await using var client = new AssuanClient(options);
    await client.ConnectAsync();

    var cmd = new AssuanCommand("HAVEKEY") { keygrip };
    var responseCollection = await client.InvokeAsync(cmd);

    var known = responseCollection.Any(response => response.Type == AssuanResponseType.Ok);
    Console.WriteLine($"Agent {(known ? "knows" : "does NOT know")} key {keygrip}");
  }
}
