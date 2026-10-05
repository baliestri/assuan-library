// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>One serial server session over an already accepted owned connection.</summary>
public sealed class ServerSession<TState> : IAsyncDisposable {
  private readonly AssuanChannel _channel;
  private readonly byte[] _commandBuffer = new byte[1000];
  private readonly ISessionHooks<TState> _hooks;
  private readonly ServerMachine _machine = new();
  private readonly ServerOptions _options;
  private readonly byte[] _outputBuffer = new byte[1000];
  private readonly PeerIdentity? _peer;
  private readonly CommandRegistry<TState> _registry;
  private readonly CancellationTokenSource _stop = new();
  private int _started;
  private TState _state;

  /// <summary>Takes ownership of the accepted stream; each session receives independent state.</summary>
  public ServerSession(AcceptedConnection accepted, TState state, CommandRegistry<TState> registry,
  ISessionHooks<TState> hooks, ServerOptions options) {
    ArgumentNullException.ThrowIfNull(accepted);
    _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
    _options = options ?? throw new ArgumentNullException(nameof(options));
    _registry.Freeze();
    _state = state;
    _peer = accepted.PeerIdentity;
    _channel = new AssuanChannel(accepted.Stream);
    Control = new SessionControl(RequestStop);
  }

  internal SessionControl Control { get; }

  /// <summary>Requests an abrupt stop without claiming an active handler has finished.</summary>
  public ValueTask DisposeAsync() {
    RequestStop();
    return ValueTask.CompletedTask;
  }

  /// <summary>Processes greeting and commands exactly once, closing the owned channel on completion.</summary>
  public Task RunAsync(CancellationToken cancellationToken = default) {
    if (Interlocked.Exchange(ref _started, 1) != 0) {
      throw new InvalidOperationException("A server session can only run once.");
    }

    return RunCoreAsync(cancellationToken);
  }

  private async Task RunCoreAsync(CancellationToken cancellationToken) {
    await using var cancellation = cancellationToken.Register(RequestStop);
    ServerException? failure = null;
    var reason = SessionEnd.Clean;
    try {
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
      var token = linked.Token;
      if (token.IsCancellationRequested) {
        throw new ServerException(ServerErrorKind.Cancelled);
      }

      var greeting = new TransportDeadline(_options.GreetingTimeout);
      var auth = new CommandContext<TState>(_state, ReadOnlyMemory<byte>.Empty, _peer, false,
        _channel, _machine, greeting, _options.MaxInquiryBytes, _options.InquiryTimeout);
      try {
        await InvokeWithinAsync(ct => _hooks.AuthenticateAsync(auth, ct), greeting, token).ConfigureAwait(false);
        _state = auth.State;
        auth.Close();
        await SendFinalAsync(null, greeting, token).ConfigureAwait(false);
      }
      catch (Exception error) when (!auth.IsClosed && _machine.State == ServerState.Greeting &&
                                    error is not (ServerException or TransportException or ProtocolException or StateException)) {
        _state = auth.State;
        auth.Close();
        await SendFinalAsync(error is HandlerException { Kind: HandlerErrorKind.Remote } remote
          ? remote
          : HandlerException.Internal(), greeting, token).ConfigureAwait(false);
        throw new ServerException(ServerErrorKind.Handler);
      }
      finally {
        _state = auth.State;
        auth.Close();
      }

      var finished = false;
      while (!finished) {
        var idle = new TransportDeadline(_options.IdleTimeout);
        do {
          var hasCommand = await _channel.ReadLineOrEofAsync(idle.Remaining, token).ConfigureAwait(false);
          if (!hasCommand) {
            finished = true;
            break;
          }

          var line = _channel.Line;
          ProtocolFields.ValidateLine(line);
          if (line.IsEmpty ||
              line[0] == (byte)'#') {
            _machine.ReceiveRequest(line.IsEmpty ? RequestKind.Empty : RequestKind.Comment);
            continue;
          }

          break;
        } while (true);

        if (finished) {
          break;
        }

        string name;
        int argumentOffset;
        int argumentLength;

        {
          var line = _channel.Line;
          var parsed = ProtocolCommand.Parse(line);
          name = Encoding.ASCII.GetString(parsed.NameBytes);
          argumentLength = parsed.Arguments.Length;
          argumentOffset = line.Length - argumentLength;
          line.CopyTo(_commandBuffer);
        }

        if (name is "D" or "END" or "CAN") {
          throw new ServerException(ServerErrorKind.Protocol);
        }

        _machine.ReceiveRequest(RequestKind.Command);
        var command = new TransportDeadline(_options.CommandTimeout);
        var context = new CommandContext<TState>(_state,
          _commandBuffer.AsMemory(argumentOffset, argumentLength), _peer, true,
          _channel, _machine, command, _options.MaxInquiryBytes, _options.InquiryTimeout);
        try {
          var handler = _registry.Find(name);
          if (ServerBuiltins.IsBuiltin(name)) {
            try {
              var bye = false;
              await InvokeWithinAsync(async ct => {
                bye = await ServerBuiltins.ExecuteAsync(name, context, _registry, _hooks, ct).ConfigureAwait(false);
              }, command, token).ConfigureAwait(false);
              finished = bye;
              await SendFinalAsync(null, command, token).ConfigureAwait(false);
            }
            catch (Exception error) when (!context.IsClosed &&
                                          _machine.State is ServerState.Handler or ServerState.AwaitFinal &&
                                          error is not (ServerException or TransportException or ProtocolException or StateException)) {
              await SendFinalAsync(error is HandlerException { Kind: HandlerErrorKind.Remote } remote
                ? remote
                : HandlerException.Internal(), command, token).ConfigureAwait(false);
            }
          }
          else if (handler is null) {
            await SendFinalAsync(HandlerException.Remote(175, "unknown command"), command, token).ConfigureAwait(false);
          }
          else {
            try {
              await InvokeWithinAsync(ct => handler.HandleAsync(context, ct), command, token).ConfigureAwait(false);
              if (context.IsClosed ||
                  _machine.State is ServerState.Inquiry or ServerState.Invalid) {
                throw new ServerException(ServerErrorKind.Protocol);
              }

              await SendFinalAsync(null, command, token).ConfigureAwait(false);
            }
            catch (Exception error) when (!context.IsClosed && _machine.State is ServerState.Handler or ServerState.AwaitFinal
                                                            && error is not (ServerException or TransportException or ProtocolException
                                                              or StateException)) {
              await SendFinalAsync(error is HandlerException { Kind: HandlerErrorKind.Remote } remote
                ? remote
                : HandlerException.Internal(), command, token).ConfigureAwait(false);
            }
          }
        }
        finally {
          _state = context.State;
          context.Close();
          CryptographicOperations.ZeroMemory(_commandBuffer);
        }
      }
    }
    catch (Exception error) {
      reason = error switch {
        ServerException { Kind: ServerErrorKind.Timeout } or TransportException { Kind: TransportErrorKind.Timeout }
          => SessionEnd.Timeout,
        ServerException { Kind: ServerErrorKind.Cancelled } or TransportException { Kind: TransportErrorKind.Cancelled }
          => SessionEnd.Cancelled,
        ProtocolException or StateException or ServerException { Kind: ServerErrorKind.Protocol }
          => SessionEnd.Protocol,
        TransportException => SessionEnd.Transport,
        var _ => SessionEnd.Handler
      };
      failure = error as ServerException ?? new ServerException(reason switch {
        SessionEnd.Timeout => ServerErrorKind.Timeout,
        SessionEnd.Cancelled => ServerErrorKind.Cancelled,
        SessionEnd.Protocol => ServerErrorKind.Protocol,
        SessionEnd.Transport => ServerErrorKind.Transport,
        var _ => ServerErrorKind.Handler
      });
    }
    finally {
      _machine.Close();
      _channel.Close();
      CryptographicOperations.ZeroMemory(_commandBuffer);
      CryptographicOperations.ZeroMemory(_outputBuffer);
      try {
        using var close = new CancellationTokenSource(_options.ClosedTimeout);
        var closing = _hooks.ClosedAsync(_state, reason, close.Token).AsTask();
        try { await closing.WaitAsync(_options.ClosedTimeout, close.Token).ConfigureAwait(false); }
        catch (Exception error) when (error is TimeoutException or OperationCanceledException) {
          // A non-cooperative close hook still owns state. Do not silently detach it.
          try { await closing.ConfigureAwait(false); }
          catch {
            /* Keep the deadline as the primary close failure. */
          }

          throw;
        }
      }
      catch {
        failure ??= new ServerException(ServerErrorKind.Handler);
      }

      _stop.Dispose();
    }

    if (failure is not null) {
      throw failure;
    }
  }

  private async Task SendFinalAsync(HandlerException? error, TransportDeadline deadline,
  CancellationToken cancellationToken) {
    try {
      var line = error is null
        ? ProtocolResponse.Ok(ReadOnlySpan<byte>.Empty)
        : ProtocolResponse.Err(error.Kind == HandlerErrorKind.Remote ? error.Code : 63,
          error.Kind == HandlerErrorKind.Remote
            ? Encoding.UTF8.GetBytes(error.PublicText!)
            : "internal error"u8.ToArray());
      var written = ProtocolEncoder.EncodeResponse(line, _outputBuffer);
      _machine.SendResponse(line.Kind);
      await _channel.WriteLineAsync(_outputBuffer.AsMemory(0, written), deadline.Remaining, cancellationToken)
        .ConfigureAwait(false);
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new ServerException(ServerErrorKind.Timeout);
      }
    }
    finally { CryptographicOperations.ZeroMemory(_outputBuffer); }
  }

  private async Task InvokeWithinAsync(Func<CancellationToken, ValueTask> callback, TransportDeadline deadline,
  CancellationToken cancellationToken) {
    if (deadline.Remaining == TimeSpan.Zero) {
      throw new ServerException(ServerErrorKind.Timeout);
    }

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    linked.CancelAfter(deadline.Remaining);
    var task = callback(linked.Token).AsTask();
    try { await task.WaitAsync(deadline.Remaining, linked.Token).ConfigureAwait(false); }
    catch (Exception error) when (error is TimeoutException or OperationCanceledException) {
      var kind = cancellationToken.IsCancellationRequested ? ServerErrorKind.Cancelled : ServerErrorKind.Timeout;
      RequestStop();
      try { await task.ConfigureAwait(false); }
      catch {
        /* The timeout/cancellation remains primary after observing the handler. */
      }

      throw new ServerException(kind);
    }
  }

  private void RequestStop() {
    try { _stop.Cancel(); }
    catch (ObjectDisposedException) { return; }

    _machine.Invalidate();
    _channel.Close();
  }
}

/// <summary>Internal host control that closes a session without releasing an active handler's state.</summary>
internal sealed class SessionControl(Action requestStop) {
  internal void RequestStop()
    => requestStop();
}
