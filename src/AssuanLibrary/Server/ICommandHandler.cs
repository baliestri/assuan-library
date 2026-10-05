// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Handles one serialized command for a session-specific application state.</summary>
public interface ICommandHandler<TState> {
  /// <summary>Case-sensitive ASCII command token.</summary>
  string Name { get; }

  /// <summary>Public, single-line HELP description.</summary>
  string? Description { get; }

  /// <summary>Emits nonfinal output; the session runner alone sends OK or ERR.</summary>
  ValueTask HandleAsync(CommandContext<TState> context, CancellationToken cancellationToken);
}
