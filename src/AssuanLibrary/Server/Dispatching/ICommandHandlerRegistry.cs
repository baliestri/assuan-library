// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Server.Dispatching;

/// <summary>
///   Registry that stores and resolves command handlers by command name.
/// </summary>
internal interface ICommandHandlerRegistry {
  /// <summary>
  ///   Adds a handler to the registry.
  /// </summary>
  /// <param name="handler">The handler to register.</param>
  /// <exception cref="InvalidOperationException">Thrown when another handler with the same command name is already registered.</exception>
  void Add(CommandHandler handler);

  /// <summary>
  ///   Tries to resolve a handler by command name.
  /// </summary>
  /// <param name="commandName">The command name to resolve.</param>
  /// <param name="handler">The resolved handler.</param>
  /// <returns><see langword="true" /> when a handler was found; otherwise <see langword="false" />.</returns>
  bool TryGet(string commandName, out CommandHandler handler);
}
