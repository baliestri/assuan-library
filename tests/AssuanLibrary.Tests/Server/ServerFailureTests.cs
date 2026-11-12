// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using AssuanLibrary.Server;
using AssuanLibrary.Tests.Transport;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class ServerFailureTests {
  [Fact]
  public async Task FactoryFailureClosesOnlyItsConnectionAndAcceptanceContinues() {
    var acceptor = new ControlledAcceptor();
    var first = new ObservedStream();
    var second = new SessionDuplexStream("BYE\n"u8.ToArray());
    var calls = 0;
    var server = new AssuanServer<object>(() => Interlocked.Increment(ref calls) == 1
        ? throw new InvalidOperationException("SECRET_MARKER")
        : new object(),
      new ServerOptions(maxSessions: 1));
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    acceptor.Enqueue(first);
    acceptor.Enqueue(second);
    await acceptor.WaitForCallsAsync(3, TestContext.Current.CancellationToken);
    await server.WhenDrainedAsync(TestContext.Current.CancellationToken);
    Assert.Equal(2, calls);
    Assert.True(first.Disposals > 0);
    Assert.Equal("OK\nOK\n"u8.ToArray(), second.WrittenBytes);
    await stop.CancelAsync();
    await serving;
  }

  [Fact]
  public async Task FactoryDiagnosticsReportCategoriesWithoutTheThrownText() {
    using var probe = new DiagnosticProbe();
    var acceptor = new ControlledAcceptor();
    var server = new AssuanServer<object>(() => throw new InvalidOperationException("SECRET_MARKER"),
      ServerOptions.Default);
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    acceptor.Enqueue(new ObservedStream());
    await acceptor.WaitForCallsAsync(2, TestContext.Current.CancellationToken);
    await server.WhenDrainedAsync(TestContext.Current.CancellationToken);
    await stop.CancelAsync();
    await serving;
    Assert.Contains(probe.Entries, value => value.Contains("Handler", StringComparison.Ordinal));
    Assert.DoesNotContain(probe.Entries, value => value.Contains("SECRET_MARKER", StringComparison.Ordinal));
  }

  [Fact]
  public async Task AcceptorFailureIsRedactedAndPreservedAfterInitiatingShutdown() {
    var first = new SessionDuplexStream("BYE\n"u8.ToArray());
    var acceptor = new FailsOnSecondAccept(first);
    var server = new AssuanServer<object>(() => new object(), ServerOptions.Default);
    var error = await Assert.ThrowsAsync<ServerException>(() =>
      server.ServeAsync(acceptor, TestContext.Current.CancellationToken));
    Assert.Equal(ServerErrorKind.Transport, error.Kind);
    Assert.IsType<TransportException>(error.TechnicalCause);
    Assert.DoesNotContain("SECRET_MARKER", error.ToString(), StringComparison.Ordinal);
    Assert.True(acceptor.Disposed);
    Assert.Equal("OK\nOK\n"u8.ToArray(), first.WrittenBytes);
  }

  [Fact]
  public async Task NoncooperativeLateAcceptIsClosedWithoutBecomingASession() {
    var acceptor = new LateAcceptor();
    var states = 0;
    var server = new AssuanServer<object>(() =>
      {
        Interlocked.Increment(ref states);
        return new object();
      },
      ServerOptions.Default);
    using var stop = new CancellationTokenSource();
    var serving = server.ServeAsync(acceptor, stop.Token);
    await acceptor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await stop.CancelAsync();
    await serving.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    var late = new ObservedStream();
    acceptor.Complete(late);
    Assert.True(await Eventually.UntilAsync(() => late.Disposals != 0,
      TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    Assert.Equal(0, states);
    Assert.True(acceptor.Disposed);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task ObservedAcceptResultHasDeterministicPriorityOverConcurrentStop(bool fail) {
    using var stop = new CancellationTokenSource();
    var stream = new ObservedStream();
    var acceptor = new StopDuringAccept(stop, stream, fail);
    var states = 0;
    var server = new AssuanServer<object>(() =>
      {
        Interlocked.Increment(ref states);
        return new object();
      },
      ServerOptions.Default);
    if (fail) {
      var error = await Assert.ThrowsAsync<ServerException>(() => server.ServeAsync(acceptor, stop.Token));
      Assert.Equal(ServerErrorKind.Transport, error.Kind);
      Assert.DoesNotContain("SECRET_MARKER", error.ToString(), StringComparison.Ordinal);
      await stream.DisposeAsync();
    }
    else {
      await server.ServeAsync(acceptor, stop.Token);
      Assert.Equal(1, stream.Disposals);
    }

    Assert.Equal(0, states);
    Assert.True(acceptor.Disposed);
  }

  private sealed class DiagnosticProbe : IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>, IDisposable {
    private readonly IDisposable _all;
    private readonly List<string> _entries = [];
    private IDisposable? _events;

    internal DiagnosticProbe() => _all = DiagnosticListener.AllListeners.Subscribe(this);

    internal string[] Entries {
      get
      {
        lock (_entries) {
          return [.. _entries];
        }
      }
    }

    public void Dispose() {
      _events?.Dispose();
      _all.Dispose();
    }

    public void OnNext(DiagnosticListener listener) {
      if (string.Equals(listener.Name, "AssuanLibrary.Server", StringComparison.Ordinal)) {
        _events = listener.Subscribe(this);
      }
    }

    public void OnError(Exception error) { }

    public void OnCompleted() { }

    public void OnNext(KeyValuePair<string, object?> entry) {
      lock (_entries) {
        _entries.Add($"{entry.Key} {entry.Value}");
      }
    }
  }

  private sealed class FailsOnSecondAccept : IAssuanAcceptor {
    private readonly AcceptedConnection _first;
    internal bool Disposed;
    private int _calls;

    internal FailsOnSecondAccept(Stream first) => _first = new AcceptedConnection(first);

    public ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default)
      => Interlocked.Increment(ref _calls) == 1
        ? ValueTask.FromResult(_first)
        : ValueTask.FromException<AcceptedConnection>(
          new TransportException(TransportErrorKind.Io, new IOException("SECRET_MARKER")));

    public ValueTask DisposeAsync() {
      Disposed = true;
      return ValueTask.CompletedTask;
    }
  }

  private sealed class LateAcceptor : IAssuanAcceptor {
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<AcceptedConnection> _late =
      new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal bool Disposed;

    public ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
      Entered.TrySetResult();
      return new ValueTask<AcceptedConnection>(_late.Task); // Ignores cancellation deliberately.
    }

    public ValueTask DisposeAsync() {
      Disposed = true;
      return ValueTask.CompletedTask;
    }

    internal void Complete(Stream stream)
      => _late.SetResult(new AcceptedConnection(stream));
  }

  private sealed class ObservedStream : MemoryStream {
    private int _disposals;
    internal int Disposals => Volatile.Read(ref _disposals);

    protected override void Dispose(bool disposing) {
      if (disposing) {
        Interlocked.Increment(ref _disposals);
      }

      base.Dispose(disposing);
    }
  }

  private sealed class StopDuringAccept(CancellationTokenSource stop, Stream stream, bool fail)
    : IAssuanAcceptor {
    internal bool Disposed;

    // Synchronous on purpose: the stop must be requested and the accept result already completed
    // within the same call, which is the race this acceptor exists to reproduce.
    public ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
      stop.Cancel();
      return fail
        ? ValueTask.FromException<AcceptedConnection>(
          new TransportException(TransportErrorKind.Io, new IOException("SECRET_MARKER")))
        : ValueTask.FromResult(new AcceptedConnection(stream));
    }

    public ValueTask DisposeAsync() {
      Disposed = true;
      return ValueTask.CompletedTask;
    }
  }
}
