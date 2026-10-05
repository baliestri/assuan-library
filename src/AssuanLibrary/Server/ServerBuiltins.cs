// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Server;

/// <summary>Reserved commands and bounded public HELP metadata.</summary>
internal static class ServerBuiltins {
  private static readonly (string Name, string Description)[] Commands = [
    ("NOP", "No operation"), ("BYE", "Close this connection"), ("HELP", "List commands"),
    ("RESET", "Reset transient session state"), ("OPTION", "Set a session option")
  ];

  internal static async ValueTask<bool> ExecuteAsync<TState>(string name, CommandContext<TState> context,
  CommandRegistry<TState> registry, ISessionHooks<TState> hooks, CancellationToken cancellationToken) {
    switch (name) {
      case "NOP":
        return false;
      case "BYE":
        return true;
      case "HELP":
        foreach (var (builtIn, description) in Commands) {
          await SendEntryAsync(context, builtIn, description, cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in registry.Entries) {
          await SendEntryAsync(context, entry.Name, entry.Description, cancellationToken).ConfigureAwait(false);
        }

        return false;
      case "RESET":
        await hooks.ResetAsync(context, cancellationToken).ConfigureAwait(false);
        return false;
      case "OPTION":
        OptionRequest request;
        try { request = OptionRequest.Parse(context.Arguments); }
        catch (ProtocolException) { throw HandlerException.Remote(55, "invalid value"); }

        await hooks.OptionAsync(request, context, cancellationToken).ConfigureAwait(false);
        return false;
      default:
        throw new InvalidOperationException("Not a built-in command.");
    }
  }

  internal static bool IsBuiltin(string name)
    => name is "NOP" or "BYE" or "HELP" or "RESET" or "OPTION";

  private static async ValueTask SendEntryAsync<TState>(CommandContext<TState> context,
  string name, string description, CancellationToken cancellationToken) {
    var bytes = new byte[997];
    var used = 0;
    try {
      foreach (var part in new[] { name, " ", description }) {
        foreach (var rune in part.EnumerateRunes()) {
          if ((used + rune.Utf8SequenceLength) > bytes.Length) {
            await context.SendCommentAsync(bytes.AsMemory(0, used), cancellationToken).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(bytes);
            used = 0;
          }

          used += rune.EncodeToUtf8(bytes.AsSpan(used));
        }
      }

      if (used != 0) {
        await context.SendCommentAsync(bytes.AsMemory(0, used), cancellationToken).ConfigureAwait(false);
      }
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
  }
}
