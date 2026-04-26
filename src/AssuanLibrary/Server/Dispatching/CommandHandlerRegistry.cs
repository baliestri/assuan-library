// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Server.Dispatching;

internal sealed class CommandHandlerRegistry : ICommandHandlerRegistry {
  private readonly Dictionary<string, CommandHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

  public CommandHandlerRegistry() { }

  public CommandHandlerRegistry(IEnumerable<CommandHandler> handlers) {
    foreach (var handler in handlers) {
      Add(handler);
    }
  }

  public void Add(CommandHandler handler) {
    if (handler is null) {
      throw new ArgumentNullException(nameof(handler));
    }

    if (_handlers.ContainsKey(handler.Name)) {
      throw new InvalidOperationException($"A handler for the command '{handler.Name}' is already registered.");
    }

    _handlers[handler.Name] = handler;
  }

  public bool TryGet(string commandName, out CommandHandler handler) {
    if (commandName is null) {
      throw new ArgumentNullException(nameof(commandName));
    }

    return _handlers.TryGetValue(commandName, out handler!);
  }
}
