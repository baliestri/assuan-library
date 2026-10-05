// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>Owns session tasks and their permits until each operation has actually finished.</summary>
internal sealed class SessionSupervisor<TState> {
  private readonly SemaphoreSlim _permits;
  private readonly HashSet<SessionWork> _sessions = [];
  private readonly Lock _sync = new();
  private TaskCompletionSource _drained = Completed();

  internal SessionSupervisor(int capacity)
    => _permits = new SemaphoreSlim(capacity, capacity);

  internal int PendingCount {
    get {
      lock (_sync) {
        return _sessions.Count;
      }
    }
  }

  internal Task AcquireAsync(CancellationToken token)
    => _permits.WaitAsync(token);

  internal void ReleasePermit()
    => _permits.Release();

  internal Task WhenDrainedAsync(CancellationToken token) {
    Task waiting;
    lock (_sync) {
      waiting = _drained.Task;
    }

    return waiting.WaitAsync(token);
  }

  internal void Track(AcceptedConnection accepted, Func<TState> factory,
  CommandRegistry<TState> registry, ISessionHooks<TState> hooks, ServerOptions options) {
    var work = new SessionWork(this);
    lock (_sync) {
      if (_sessions.Count == 0) {
        _drained = NewSource();
      }

      _sessions.Add(work);
    }

    work.Start(accepted, factory, registry, hooks, options);
  }

  internal void RequestStopAll() {
    SessionWork[] snapshot;
    lock (_sync) {
      snapshot = [.. _sessions];
    }

    foreach (var session in snapshot) {
      session.RequestStop();
    }
  }

  private void Completed(SessionWork session) {
    lock (_sync) {
      _sessions.Remove(session);
      _permits.Release();
      if (_sessions.Count == 0) {
        _drained.TrySetResult();
      }
    }
  }

  private static TaskCompletionSource Completed() {
    var result = NewSource();
    result.SetResult();
    return result;
  }

  private static TaskCompletionSource NewSource()
    => new(TaskCreationOptions.RunContinuationsAsynchronously);

  private sealed class SessionWork(SessionSupervisor<TState> owner) {
    private readonly CancellationTokenSource _stop = new();
    private SessionControl? _control;
    internal Task? Task { get; private set; }

    internal void Start(AcceptedConnection accepted, Func<TState> factory,
    CommandRegistry<TState> registry, ISessionHooks<TState> hooks, ServerOptions options) {
      Task = Task.Run(async () => {
        var transferred = false;
        try {
          if (_stop.IsCancellationRequested) {
            return;
          }

          var state = factory();
          if (_stop.IsCancellationRequested) {
            return;
          }

          var session = new ServerSession<TState>(accepted, state, registry, hooks, options);
          transferred = true;
          Volatile.Write(ref _control, session.Control);
          if (_stop.IsCancellationRequested) {
            session.Control.RequestStop();
          }

          await session.RunAsync(_stop.Token).ConfigureAwait(false);
        }
        catch (Exception error) {
          ServerDiagnostics.Report(error is ServerException typed ? typed.Kind : ServerErrorKind.Handler);
        }
        finally {
          if (!transferred) {
            try { await accepted.Stream.DisposeAsync().ConfigureAwait(false); }
            catch { ServerDiagnostics.Report(ServerErrorKind.Transport); }
          }

          _stop.Dispose();
          owner.Completed(this);
        }
      });
    }

    internal void RequestStop() {
      try { _stop.Cancel(); }
      catch (ObjectDisposedException) { return; }

      Volatile.Read(ref _control)?.RequestStop();
    }
  }
}
