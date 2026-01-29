// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Server;

/// <inheritdoc />
public sealed class AssuanServer(
  IAssuanListenerFactory listenerFactory,
  IAssuanEndpoint endpoint,
  ICommandDispatcher commandDispatcher,
  AssuanServerOptions options
) : IAssuanServer {
  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint.
  /// </summary>
  /// <param name="endpoint">The endpoint to use for the server.</param>
  public AssuanServer(IAssuanEndpoint endpoint)
    : this(CreateDefaultFactory(AssuanServerOptions.Default), endpoint, new CommandDispatcher(), AssuanServerOptions.Default) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint and command dispatcher.
  /// </summary>
  /// <param name="endpoint">The endpoint to use for the server.</param>
  /// <param name="commandDispatcher">The command dispatcher to use for handling commands.</param>
  public AssuanServer(IAssuanEndpoint endpoint, ICommandDispatcher commandDispatcher)
    : this(CreateDefaultFactory(AssuanServerOptions.Default), endpoint, commandDispatcher, AssuanServerOptions.Default) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint and options.
  /// </summary>
  /// <param name="endpoint">The endpoint to use for the server.</param>
  /// <param name="options">The configuration options for the server.</param>
  public AssuanServer(IAssuanEndpoint endpoint, AssuanServerOptions options)
    : this(CreateDefaultFactory(options), endpoint, new CommandDispatcher(), options) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanServer" /> class with the specified endpoint, command dispatcher, and options.
  /// </summary>
  /// <param name="endpoint">The endpoint to use for the server.</param>
  /// <param name="commandDispatcher">The command dispatcher to use for handling commands.</param>
  /// <param name="options">The configuration options for the server.</param>
  public AssuanServer(IAssuanEndpoint endpoint, ICommandDispatcher commandDispatcher, AssuanServerOptions options)
    : this(CreateDefaultFactory(options), endpoint, commandDispatcher, options) { }

  /// <inheritdoc />
  public void Run() {
    var listener = listenerFactory.CreateListener(endpoint);

    do {
      var connection = listener.Accept();
      HandleSession(connection);
    }
    while (true);
  }

  /// <inheritdoc />
  public async Task RunAsync(CancellationToken ct = default) {
    var listener = listenerFactory.CreateListener(endpoint);

    do {
      var connection = await listener.AcceptAsync(ct).ConfigureAwait(false);
      await HandleSessionAsync(connection, ct).ConfigureAwait(false);
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

  private async Task HandleSessionAsync(IAssuanConnection connection, CancellationToken ct = default) {
    var session = new ServerSession(ct);
    var context = new ServerContext(connection, session);

    if (options.OnAuthenticateSessionAsync is not null) {
      await options.OnAuthenticateSessionAsync(context).ConfigureAwait(false);
    }

    if (options.SendBanner) {
      var bannerResponse = AssuanResponse.Ok(options.Banner);
      await context.SendResponseAsync(bannerResponse, ct);
    }

    while (connection.IsConnected &&
           !ct.IsCancellationRequested) {
      var buffer = await connection.ReadAsync(ct).ConfigureAwait(false);

      if (buffer.IsEmpty) {
        continue;
      }

      session.RefreshLastActivity();

      var command = new AssuanCommand(buffer.ToArray());
      await commandDispatcher.DispatchAsync(command, context).ConfigureAwait(false);
    }

    session.Dispose();
    await connection.DisposeAsync();
  }

  private void HandleSession(IAssuanConnection connection, CancellationToken ct = default) {
    var session = new ServerSession(ct);
    var context = new ServerContext(connection, session);

    options.OnAuthenticateSessionAsync?.Invoke(context).GetAwaiter().GetResult();

    if (options.SendBanner) {
      var bannerResponse = AssuanResponse.Ok(options.Banner);
      context.SendResponse(bannerResponse);
    }

    while (connection.IsConnected &&
           !ct.IsCancellationRequested) {
      var buffer = connection.Read();

      if (buffer.Length == 0) {
        continue;
      }

      session.RefreshLastActivity();

      var command = new AssuanCommand(buffer.ToArray());
      commandDispatcher.Dispatch(command, context);
    }

    session.Dispose();
    connection.Dispose();
  }
}
