// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol.Abstractions;

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   Represents a dispatcher for Assuan commands on server side.
/// </summary>
public interface ICommandDispatcher {
  /// <summary>
  ///   Tries to add a new command handler to the dispatcher.
  /// </summary>
  /// <param name="handler">The command handler to add.</param>
  /// <returns><see langword="true" /> if the handler was added successfully; otherwise, <see langword="false" />.</returns>
  bool TryAdd(CommandHandler handler);

  /// <summary>
  ///   Dispatches the given command to the appropriate handler.
  /// </summary>
  /// <param name="command">The command to dispatch.</param>
  /// <param name="context">The server context.</param>
  void Dispatch(IReadOnlyAssuanCommand command, IServerContext context);

  /// <summary>
  ///   Asynchronously dispatches the given command to the appropriate handler.
  /// </summary>
  /// <param name="command">The command to dispatch.</param>
  /// <param name="context">The server context.</param>
  /// <returns>A task that represents the asynchronous operation.</returns>
  Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context);
}
