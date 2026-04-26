// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Server.Dispatching;
using AssuanLibrary.Server.Sessions;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Server;

/// <inheritdoc />
public sealed class AssuanServer(IAssuanListenerFactory listenerFactory, ICommandDispatcher commandDispatcher, AssuanServerOptions options)
  : IAssuanServer {
  private readonly AssuanSessionRunner _sessionRunner = new(commandDispatcher, options);

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint factory.
  /// </summary>
  /// <param name="listenerFactory">The listener factory to create listeners for incoming connections.</param>
  public AssuanServer(IAssuanListenerFactory listenerFactory)
    : this(listenerFactory, new CommandDispatcher([]), AssuanServerOptions.Default) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint factory and options.
  /// </summary>
  /// <param name="listenerFactory">The listener factory to create listeners for incoming connections.</param>
  /// <param name="options">The configuration options for the server.</param>
  public AssuanServer(IAssuanListenerFactory listenerFactory, AssuanServerOptions options)
    : this(listenerFactory, new CommandDispatcher([]), options) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint and command dispatcher.
  /// </summary>
  /// <param name="commandDispatcher">The command dispatcher to use for handling commands.</param>
  public AssuanServer(ICommandDispatcher commandDispatcher)
    : this(CreateDefaultFactory(AssuanServerOptions.Default), commandDispatcher, AssuanServerOptions.Default) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint, command dispatcher, and options.
  /// </summary>
  /// <param name="commandDispatcher">The command dispatcher to use for handling commands.</param>
  /// <param name="options">The configuration options for the server.</param>
  public AssuanServer(ICommandDispatcher commandDispatcher, AssuanServerOptions options)
    : this(CreateDefaultFactory(options), commandDispatcher, options) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint and options.
  /// </summary>
  /// <param name="options">The configuration options for the server.</param>
  public AssuanServer(AssuanServerOptions options)
    : this(CreateDefaultFactory(options), new CommandDispatcher([]), options) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with default settings.
  /// </summary>
  public AssuanServer()
    : this(CreateDefaultFactory(AssuanServerOptions.Default), new CommandDispatcher([]), AssuanServerOptions.Default) { }

  /// <inheritdoc />
  public void Run(IAssuanEndpoint endpoint) {
    var listener = listenerFactory.CreateListener(endpoint);

    do {
      var connection = listener.Accept();
      _sessionRunner.Run(connection);
    }
    while (true);
  }

  /// <inheritdoc />
  public async Task RunAsync(IAssuanEndpoint endpoint, CancellationToken ct = default) {
    var listener = listenerFactory.CreateListener(endpoint);

    do {
      var connection = await listener.AcceptAsync(ct).ConfigureAwait(false);
      await _sessionRunner.RunAsync(connection, ct).ConfigureAwait(false);
    }
    while (!ct.IsCancellationRequested);
  }

  /// <inheritdoc />
  public void RegisterCommandHandler(CommandHandler commandHandler) {
    if (!commandDispatcher.TryAdd(commandHandler)) {
      throw new InvalidOperationException($"A handler for the command '{commandHandler.Name}' is already registered.");
    }
  }

  /// <inheritdoc />
  public void RegisterCommandHandler<TCommandHandler>() where TCommandHandler : CommandHandler, new() {
    var handler = new TCommandHandler();

    if (!commandDispatcher.TryAdd(handler)) {
      throw new InvalidOperationException($"A handler for the command '{handler.Name}' is already registered.");
    }
  }

  private static IAssuanListenerFactory CreateDefaultFactory(AssuanServerOptions options)
    => new DefaultListenerFactory(options);
}
