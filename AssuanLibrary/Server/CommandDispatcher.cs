// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Server;

internal sealed class CommandDispatcher(HashSet<CommandHandler> commandHandlers) : ICommandDispatcher {
  private readonly Dictionary<string, CommandHandler> _commandHandlers = commandHandlers
    .ToDictionary(handler => handler.Name, StringComparer.OrdinalIgnoreCase);

  public CommandDispatcher() : this([]) { }

  /// <inheritdoc />
  public bool TryAdd(CommandHandler handler) {
    if (_commandHandlers.ContainsKey(handler.Name)) {
      return false;
    }

    _commandHandlers[handler.Name] = handler;
    return true;
  }

  /// <inheritdoc />
  public void Dispatch(IReadOnlyAssuanCommand command, IServerContext context) {
    if (!_commandHandlers.TryGetValue(command.Name, out var commandHandler)) {
      context.SendResponse(AssuanResponse.Error(67109139, "Unknown command"));
      return;
    }

    commandHandler.Handle(command, context);
  }

  /// <inheritdoc />
  public async Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context) {
    if (!_commandHandlers.TryGetValue(command.Name, out var commandHandler)) {
      await context.SendResponseAsync(AssuanResponse.Error(67109139, "Unknown command"), context.Session.CancellationToken).ConfigureAwait(false);
      return;
    }

    await commandHandler.HandleAsync(command, context).ConfigureAwait(false);
  }
}
