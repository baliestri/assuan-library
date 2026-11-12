// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Server;

/// <summary>Application policy hooks, invoked serially over each session's state.</summary>
public interface ISessionHooks<TState> {
  /// <summary>Runs before greeting OK; may emit status or perform an inquiry.</summary>
  ValueTask AuthenticateAsync(CommandContext<TState> context, CancellationToken cancellationToken);

  /// <summary>Handles a validated OPTION request.</summary>
  ValueTask OptionAsync(OptionRequest request, CommandContext<TState> context, CancellationToken cancellationToken);

  /// <summary>Clears transient application state, retaining the library's authentication flag.</summary>
  [SuppressMessage("ReSharper", "UnusedParameter.Global",
    Justification = "Public hook contract; application implementations consume every parameter.")]
  ValueTask ResetAsync(CommandContext<TState> context, CancellationToken cancellationToken);

  /// <summary>Observes closure after the active handler finishes and the channel closes.</summary>
  [SuppressMessage("ReSharper", "UnusedParameter.Global",
    Justification = "Public hook contract; application implementations consume every parameter.")]
  ValueTask ClosedAsync(TState state, SessionEnd reason, CancellationToken cancellationToken);
}
