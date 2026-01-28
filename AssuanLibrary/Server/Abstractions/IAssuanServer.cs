// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   A server that listens for Assuan protocol connections and handles commands.
/// </summary>
public interface IAssuanServer : IAsyncDisposable, IDisposable {
  /// <summary>
  ///   Indicates whether the server is currently running.
  /// </summary>
  bool IsRunning { get; }

  /// <summary>
  ///   Starts the Assuan server to listen for incoming connections.
  /// </summary>
  void Run();

  /// <summary>
  ///   Starts the Assuan server to listen for incoming connections asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous operation.</returns>
  Task RunAsync(CancellationToken ct = default);

  /// <summary>
  ///   Registers a new command handler in the server command dispatcher.
  /// </summary>
  /// <param name="commandHandler">The command handler to register.</param>
  void RegisterCommandHandler(CommandHandler commandHandler);

  /// <summary>
  ///   Registers a new command handler of type <typeparamref name="TCommandHandler" /> in the server command dispatcher.
  /// </summary>
  /// <typeparam name="TCommandHandler">The type of the command handler to register.</typeparam>
  void RegisterCommandHandler<TCommandHandler>() where TCommandHandler : CommandHandler, new();
}
