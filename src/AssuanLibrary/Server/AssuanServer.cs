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
  private int _state = (int)AssuanServerState.Stopped;

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
    BeginRun();

    var listener = listenerFactory.CreateListener(endpoint);

    try {
      SetState(AssuanServerState.Running);

      do {
        var connection = listener.Accept();
        _sessionRunner.Run(connection);
      }
      while (true);
    }
    finally {
      EndRun();
    }
  }

  /// <inheritdoc />
  public async Task RunAsync(IAssuanEndpoint endpoint, CancellationToken ct = default) {
    BeginRun();

    if (options.MaxConcurrentSessions <= 0) {
      EndRun();
      throw new ArgumentOutOfRangeException(nameof(options.MaxConcurrentSessions), options.MaxConcurrentSessions,
        "MaxConcurrentSessions must be greater than zero.");
    }

    var listener = listenerFactory.CreateListener(endpoint);
    using var sessionConcurrencySemaphore = new SemaphoreSlim(options.MaxConcurrentSessions, options.MaxConcurrentSessions);
    var runningSessionTasks = new List<Task>();
    var sessionFailure = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

    try {
      SetState(AssuanServerState.Running);

      while (!ct.IsCancellationRequested) {
        await WaitForSessionSlotAsync(sessionConcurrencySemaphore, sessionFailure.Task, ct).ConfigureAwait(false);

        IAssuanConnection? connection;

        try {
          connection = await listener.AcceptAsync(ct).ConfigureAwait(false);
        }
        catch {
          sessionConcurrencySemaphore.Release();
          throw;
        }

        runningSessionTasks.Add(RunSessionAsync(connection, sessionConcurrencySemaphore, sessionFailure, ct));
      }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) {
      // Graceful shutdown requested.
    }
    finally {
      SetState(AssuanServerState.Stopping);
      await Task.WhenAll(runningSessionTasks).ConfigureAwait(false);
      EndRun();
    }
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

  private async Task RunSessionAsync(IAssuanConnection connection, SemaphoreSlim sessionConcurrencySemaphore,
  TaskCompletionSource<object?> sessionFailure, CancellationToken ct) {
    try {
      await _sessionRunner.RunAsync(connection, ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) {
      // Graceful shutdown requested.
    }
    catch (Exception ex) {
      if (!options.ContinueOnSessionError) {
        sessionFailure.TrySetException(ex);
      }
    }
    finally {
      sessionConcurrencySemaphore.Release();
    }
  }

  private static async Task WaitForSessionSlotAsync(SemaphoreSlim sessionConcurrencySemaphore, Task sessionFailureTask, CancellationToken ct) {
    var waitForSlotTask = sessionConcurrencySemaphore.WaitAsync(ct);
    var completedTask = await Task.WhenAny(waitForSlotTask, sessionFailureTask).ConfigureAwait(false);

    if (ReferenceEquals(completedTask, sessionFailureTask)) {
      await sessionFailureTask.ConfigureAwait(false);
    }

    await waitForSlotTask.ConfigureAwait(false);

    if (sessionFailureTask.IsCompleted) {
      await sessionFailureTask.ConfigureAwait(false);
    }
  }

  private void BeginRun() {
    if (Interlocked.CompareExchange(ref _state, (int)AssuanServerState.Starting, (int)AssuanServerState.Stopped)
        != (int)AssuanServerState.Stopped) {
      throw new InvalidOperationException("This AssuanServer instance is already running.");
    }
  }

  private void EndRun()
    => SetState(AssuanServerState.Stopped);

  private void SetState(AssuanServerState state)
    => Volatile.Write(ref _state, (int)state);

  private static IAssuanListenerFactory CreateDefaultFactory(AssuanServerOptions options)
    => new DefaultListenerFactory(options);
}
