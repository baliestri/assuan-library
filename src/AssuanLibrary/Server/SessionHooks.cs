// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Payload-free reason for session closure.</summary>
public enum SessionEnd { Clean, Protocol, Transport, Cancelled, Timeout, Handler }

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
