// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

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
