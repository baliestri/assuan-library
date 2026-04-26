// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Server.Dispatching;

internal sealed class CommandDispatcher(ICommandHandlerRegistry commandHandlerRegistry) : ICommandDispatcher {
  public CommandDispatcher(IEnumerable<CommandHandler> handlers) : this(new CommandHandlerRegistry(handlers)) { }

  public bool TryAdd(CommandHandler handler) {
    try {
      commandHandlerRegistry.Add(handler);
      return true;
    }
    catch (InvalidOperationException) {
      return false;
    }
  }

  public void Dispatch(IReadOnlyAssuanCommand command, IServerContext context) {
    if (!commandHandlerRegistry.TryGet(command.Name, out var commandHandler)) {
      context.SendResponse(AssuanResponse.Error(175, "Unknown command"));
      return;
    }

    commandHandler.Handle(command, context);
  }

  public async Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context) {
    if (!commandHandlerRegistry.TryGet(command.Name, out var commandHandler)) {
      await context.SendResponseAsync(AssuanResponse.Error(175, "Unknown command"), context.Session.CancellationToken).ConfigureAwait(false);
      return;
    }

    await commandHandler.HandleAsync(command, context).ConfigureAwait(false);
  }
}
