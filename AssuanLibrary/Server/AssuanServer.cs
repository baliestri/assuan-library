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
  private bool _disposed;
  private IAssuanListener? _listener;

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
  public bool IsRunning => _listener is { IsListening: true };

  /// <inheritdoc />
  public void Run() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanServer));

    _listener = listenerFactory.CreateListener(endpoint);

    do {
      var connection = _listener.Accept();
      HandleSession(connection);
    }
    while (IsRunning);
  }

  /// <inheritdoc />
  public async Task RunAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanServer));

    _listener = listenerFactory.CreateListener(endpoint);

    do {
      var connection = await _listener.AcceptAsync(ct).ConfigureAwait(false);
      await HandleSessionAsync(connection, ct).ConfigureAwait(false);
    }
    while (IsRunning && !ct.IsCancellationRequested);
  }

  /// <inheritdoc />
  public void RegisterCommandHandler(CommandHandler commandHandler) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanServer));

    if (!commandDispatcher.TryAdd(commandHandler)) {
      throw new InvalidOperationException($"A handler for the command '{commandHandler.Name}' is already registered.");
    }
  }

  /// <inheritdoc />
  public void RegisterCommandHandler<TCommandHandler>() where TCommandHandler : CommandHandler, new() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanServer));

    var handler = new TCommandHandler();

    if (!commandDispatcher.TryAdd(handler)) {
      throw new InvalidOperationException($"A handler for the command '{handler.Name}' is already registered.");
    }
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _listener?.Dispose();
    _listener = null;
    _disposed = true;
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    if (_listener is not null) {
      await _listener.DisposeAsync().ConfigureAwait(false);
    }

    _listener = null;
    _disposed = true;
  }

  private static IAssuanListenerFactory CreateDefaultFactory(AssuanServerOptions options)
    => new DefaultListenerFactory(options);

  private async Task HandleSessionAsync(IAssuanConnection connection, CancellationToken ct = default) {
    using var session = new ServerSession(ct);
    var context = new ServerContext(connection, session);

    if (options.OnAuthenticateSessionAsync is not null) {
      await options.OnAuthenticateSessionAsync(context).ConfigureAwait(false);
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
  }

  private void HandleSession(IAssuanConnection connection, CancellationToken ct = default) {
    using var session = new ServerSession(ct);
    var context = new ServerContext(connection, session);

    options.OnAuthenticateSessionAsync?.Invoke(context).GetAwaiter().GetResult();

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
  }
}
