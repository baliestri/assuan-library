// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>Configures one bounded concurrent server over an owned acceptor.</summary>
public sealed class AssuanServer<TState> : IAsyncDisposable {
  private readonly Func<TState> _factory;
  private readonly Func<TimeSpan, CancellationToken, Task> _graceDelay;
  private readonly ServerOptions _options;
  private readonly CommandRegistry<TState> _registry = new();
  private readonly SessionSupervisor<TState> _supervisor;
  private CancellationTokenSource? _admissionStop;
  private int _disposed;
  private ISessionHooks<TState> _hooks = new DefaultSessionHooks<TState>();
  private int _started;

  /// <summary>Stores a per-connection state factory and validated limits.</summary>
  public AssuanServer(Func<TState> stateFactory, ServerOptions options)
    : this(stateFactory, options, (delay, token) => Task.Delay(delay, token)) { }

  internal AssuanServer(Func<TState> stateFactory, ServerOptions options,
  Func<TimeSpan, CancellationToken, Task> graceDelay) {
    _factory = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
    _options = options ?? throw new ArgumentNullException(nameof(options));
    _graceDelay = graceDelay ?? throw new ArgumentNullException(nameof(graceDelay));
    _supervisor = new SessionSupervisor<TState>(options.MaxSessions);
  }

  /// <summary>Number of admitted sessions whose handlers or close hooks have not finished.</summary>
  public int PendingSessionCount => _supervisor.PendingCount;

  /// <summary>Requests that active operations stop without claiming handlers have terminated.</summary>
  public ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) != 0) {
      return ValueTask.CompletedTask;
    }

    try { Volatile.Read(ref _admissionStop)?.Cancel(); }
    catch (ObjectDisposedException) {
      /* The admission loop has already stopped. */
    }

    _supervisor.RequestStopAll();
    return ValueTask.CompletedTask;
  }

  /// <summary>Registers a handler before any sessions are accepted.</summary>
  public void Register(ICommandHandler<TState> handler) {
    CheckConfigurable();
    _registry.Register(handler);
  }

  /// <summary>Configures a shared policy before serving begins.</summary>
  public void SetHooks(ISessionHooks<TState> hooks) {
    CheckConfigurable();
    _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
  }

  /// <summary>Starts a single owned accept loop, stopping admission when signalled.</summary>
  public Task ServeAsync(IAssuanAcceptor acceptor, CancellationToken shutdown = default) {
    ArgumentNullException.ThrowIfNull(acceptor);
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    if (Interlocked.Exchange(ref _started, 1) != 0) {
      throw new InvalidOperationException("The server has already started serving.");
    }

    ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    _registry.Freeze();
    return ServeCoreAsync(acceptor, shutdown);
  }

  /// <summary>Waits for supervised sessions to finish without cancelling or abandoning them.</summary>
  public Task WhenDrainedAsync(CancellationToken cancellationToken = default)
    => _supervisor.WhenDrainedAsync(cancellationToken);

  private async Task ServeCoreAsync(IAssuanAcceptor acceptor, CancellationToken shutdown) {
    using var accepting = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
    Volatile.Write(ref _admissionStop, accepting);
    if (Volatile.Read(ref _disposed) != 0) {
      await accepting.CancelAsync();
    }

    ServerException? acceptFailure = null;
    try {
      while (!accepting.IsCancellationRequested) {
        try { await _supervisor.AcquireAsync(accepting.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (accepting.IsCancellationRequested) { break; }

        var permitOwned = true;
        try {
          if (accepting.IsCancellationRequested) {
            break;
          }

          var pending = acceptor.AcceptAsync(accepting.Token).AsTask();
          AcceptedConnection accepted;
          try { accepted = await pending.WaitAsync(accepting.Token).ConfigureAwait(false); }
          catch (OperationCanceledException) when (accepting.IsCancellationRequested) {
            ObserveLateAccept(pending);
            break;
          }

          if (accepting.IsCancellationRequested) {
            await accepted.Stream.DisposeAsync().ConfigureAwait(false);
            break;
          }

          _supervisor.Track(accepted, _factory, _registry, _hooks, _options);
          permitOwned = false;
        }
        catch (Exception error) {
          if (accepting.IsCancellationRequested &&
              error is OperationCanceledException or
                TransportException { Kind: TransportErrorKind.Cancelled or TransportErrorKind.Closed }) {
            break;
          }

          acceptFailure = new ServerException(ServerErrorKind.Transport, error);
          break;
        }
        finally {
          if (permitOwned) {
            _supervisor.ReleasePermit();
          }
        }
      }
    }
    finally {
      await accepting.CancelAsync();
      Volatile.Write(ref _admissionStop, null);
      using var graceCancellation = new CancellationTokenSource();
      var waitForSessions = _supervisor.WhenDrainedAsync(CancellationToken.None);
      var grace = waitForSessions.IsCompleted
        ? null
        : _graceDelay(_options.ShutdownGracePeriod, graceCancellation.Token);
      try { await acceptor.DisposeAsync().ConfigureAwait(false); }
      catch (Exception error) { acceptFailure ??= new ServerException(ServerErrorKind.Transport, error); }

      if (grace is not null) {
        if (await Task.WhenAny(waitForSessions, grace).ConfigureAwait(false) == grace) {
          try { await grace.ConfigureAwait(false); }
          catch (Exception error) when (error is OperationCanceledException) {
            /* Drained early. */
          }

          _supervisor.RequestStopAll();
        }
        else { await graceCancellation.CancelAsync(); }
      }
    }

    if (acceptFailure is not null) {
      throw acceptFailure;
    }
  }

  private static void ObserveLateAccept(Task<AcceptedConnection> pending) {
    _ = pending.ContinueWith(async completed => {
      if (completed.IsCompletedSuccessfully) {
        try { await completed.Result.Stream.DisposeAsync().ConfigureAwait(false); }
        catch { ServerDiagnostics.Report(ServerErrorKind.Transport); }
      }
      else { _ = completed.Exception; }
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
  }

  private void CheckConfigurable() {
    ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    if (Volatile.Read(ref _started) != 0) {
      throw new InvalidOperationException("Server configuration is frozen.");
    }
  }
}
