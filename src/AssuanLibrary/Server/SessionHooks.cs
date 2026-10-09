// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Payload-free reason for session closure.</summary>
public enum SessionEnd {
  /// <summary>The peer completed its session or disconnected between commands.</summary>
  Clean,

  /// <summary>Malformed input or incomplete protocol work.</summary>
  Protocol,

  /// <summary>Transport read or write failed.</summary>
  Transport,

  /// <summary>Operation was cancelled.</summary>
  Cancelled,

  /// <summary>A total operation deadline expired.</summary>
  Timeout,

  /// <summary>Authentication or application handler failed.</summary>
  Handler
}

/// <summary>Application policy hooks, invoked serially over each session's state.</summary>
public interface ISessionHooks<TState> {
  /// <summary>Runs before greeting OK; may emit status or perform an inquiry.</summary>
  ValueTask AuthenticateAsync(CommandContext<TState> context, CancellationToken cancellationToken);

  /// <summary>Handles a validated OPTION request.</summary>
  ValueTask OptionAsync(OptionRequest request, CommandContext<TState> context, CancellationToken cancellationToken);

  /// <summary>Clears transient application state, retaining the library's authentication flag.</summary>
  ValueTask ResetAsync(CommandContext<TState> context, CancellationToken cancellationToken);

  /// <summary>Observes closure after the active handler finishes and the channel closes.</summary>
  ValueTask ClosedAsync(TState state, SessionEnd reason, CancellationToken cancellationToken);
}

/// <summary>Accepts an admitted stream without asserting identity; rejects unknown options.</summary>
public sealed class DefaultSessionHooks<TState> : ISessionHooks<TState> {
  /// <inheritdoc />
  public ValueTask AuthenticateAsync(CommandContext<TState> context, CancellationToken cancellationToken)
    => ValueTask.CompletedTask;

  /// <inheritdoc />
  public ValueTask OptionAsync(OptionRequest request, CommandContext<TState> context,
  CancellationToken cancellationToken)
    => ValueTask.FromException(HandlerException.Remote(174, "unknown option"));

  /// <inheritdoc />
  public ValueTask ResetAsync(CommandContext<TState> context, CancellationToken cancellationToken)
    => ValueTask.CompletedTask;

  /// <inheritdoc />
  public ValueTask ClosedAsync(TState state, SessionEnd reason, CancellationToken cancellationToken)
    => ValueTask.CompletedTask;
}
