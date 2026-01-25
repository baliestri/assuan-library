// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text;
using AssuanLibrary.Client;
using AssuanLibrary.Endpoints;
using AssuanLibrary.Expressions;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Sample;

internal static class Program {
  private static async Task Main() {
    const string KEYGRIP = "A1B2C3D4F5G6..."; // ← replace with a real keygrip from your setup
    var kind = AssuanEndpointKind.AGENT;
    var options = AssuanClientOptions.Default;

    await ShowVersionAndOtherInfoAsync(kind, options);
    await ListSecretKeysAsync(kind, options);
    await GetKeyInfoAsync(KEYGRIP, kind, options);
    await SignChallengeAsync(KEYGRIP, kind, options);
    await GetPassphraseAsync(kind, options);
    await GetPinentryConfirmationAsync(kind, options);
    await CheckKeyAvailabilityAsync(KEYGRIP, kind, options);

    await Task.CompletedTask;
  }

  private static async Task ShowVersionAndOtherInfoAsync(AssuanEndpointKind kind, AssuanClientOptions options) {
    await using var client = new AssuanClient(kind, options);
    await client.ConnectAsync();

    var versionCommand = new AssuanCommand("GETINFO") { "version" };
    var versionResponseCollection = await client.InvokeAsync(versionCommand);
    Console.WriteLine("gpg-agent version:");
    foreach (var response in versionResponseCollection) {
      Console.WriteLine($"\t→ {response:GT}");
    }

    var pidCommand = new AssuanCommand("GETINFO") { "pid" };
    var pidResponseCollection = await client.InvokeAsync(pidCommand);
    Console.WriteLine("Agent PID:");
    foreach (var response in pidResponseCollection) {
      Console.WriteLine($"\t→ {response:GT}");
    }
  }

  private static async Task ListSecretKeysAsync(AssuanEndpointKind kind, AssuanClientOptions options) {
    await using var client = new AssuanClient(kind, options);
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
      Console.WriteLine($"\t→ {response:GT} ");
    }
  }

  private static async Task GetKeyInfoAsync(string keygrip, AssuanEndpointKind kind, AssuanClientOptions options) {
    await using var client = new AssuanClient(kind, options);
    await client.ConnectAsync();

    var cmd = new AssuanCommand("KEYINFO") { keygrip };
    var responseCollection = await client.InvokeAsync(cmd);

    Console.WriteLine($"Key info for {keygrip}:");
    foreach (var response in responseCollection) {
      Console.WriteLine($"\t→ {response:GT} ");
    }
  }

  private static async Task SignChallengeAsync(string keygrip, AssuanEndpointKind kind, AssuanClientOptions options) {
    await using var client = new AssuanClient(kind, options);
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
        Console.WriteLine($"\t→ {response:GT} ");
      }

      return;
    }

    var setHashCommand = new AssuanCommand("SETHASH") { "--hash=sha256", hexValue };
    var setHashResponseCollection = await client.InvokeAsync(setHashCommand);
    if (setHashResponseCollection.Count > 1 ||
        setHashResponseCollection.Any(response => response.Type == AssuanResponseType.Error)) {
      Console.WriteLine("SETHASH command failed:");
      foreach (var response in setHashResponseCollection) {
        Console.WriteLine($"\t→ {response:GT} ");
      }

      return;
    }

    var pkSignCommand = new AssuanCommand("PKSIGN");
    var pkSignResponseCollection = await client.InvokeAsync(pkSignCommand);
    if (!pkSignResponseCollection.Any(response => response.Type == AssuanResponseType.Data)) {
      Console.WriteLine("PKSIGN command failed:");
      foreach (var response in pkSignResponseCollection) {
        Console.WriteLine($"\t→ {response:GT} ");
      }

      return;
    }

    var symbolicExpressionResponse = pkSignResponseCollection.FirstOrDefault(response => response.Type == AssuanResponseType.Data);

    if (SymbolicExpressionParser.TryParse(symbolicExpressionResponse, out var expr, out var _)) {
      Console.WriteLine("PKSIGN response (signature):");
      Console.WriteLine(SymbolicExpression.PrettyPrint(expr));
    }
  }

  private static async Task GetPassphraseAsync(AssuanEndpointKind kind, AssuanClientOptions options) {
    await using var client = new AssuanClient(kind, options);
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
      Console.WriteLine($"\t→ {response:DT} ");
    }
  }

  private static async Task GetPinentryConfirmationAsync(AssuanEndpointKind kind, AssuanClientOptions options) {
    await using var client = new AssuanClient(kind, options);
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
      Console.WriteLine($"\t→ {response:GT} ");
    }
  }

  private static async Task CheckKeyAvailabilityAsync(string keygrip, AssuanEndpointKind kind, AssuanClientOptions options) {
    await using var client = new AssuanClient(kind, options);
    await client.ConnectAsync();

    var cmd = new AssuanCommand("HAVEKEY") { keygrip };
    var responseCollection = await client.InvokeAsync(cmd);

    var known = responseCollection.Any(response => response.Type == AssuanResponseType.Ok);
    Console.WriteLine($"Agent {(known ? "knows" : "does NOT know")} key {keygrip}");
  }
}
