// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Diagnostics;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>Exclusive command response stream, unfinished disposal closes the connection.</summary>
public sealed class ClientTransaction : IAsyncDisposable {
  private readonly ClientCore _core;
  private readonly TransportDeadline _deadline;
  private readonly ClientOptions _options;
  private readonly PayloadSensitivity _sensitivity;
  private int _disposed;
  private bool _finalSeen;
  private bool _finished;
  private int _leaseActive;
  private int _reading;
  private uint? _remoteCode;

  internal ClientTransaction(ClientCore core, TransportDeadline deadline, PayloadSensitivity sensitivity,
  ClientOptions options) {
    _core = core;
    _deadline = deadline;
    _sensitivity = sensitivity;
    _options = options;
  }

  /// <summary>Abandoning a transaction without a final response invalidates the client.</summary>
  public ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0) {
      if (!_finalSeen ||
          Volatile.Read(ref _leaseActive) != 0 ||
          Volatile.Read(ref _reading) != 0) {
        _core.Invalidate();
      }

      Interlocked.Exchange(ref _core.TransactionActive, 0);
    }

    return ValueTask.CompletedTask;
  }

  /// <summary>Reads the next borrowed event, or null after the final response has been observed.</summary>
  public async Task<ClientEventLease?> ReadNextAsync(CancellationToken cancellationToken = default) {
    if (Volatile.Read(ref _disposed) != 0) {
      throw new ObjectDisposedException(nameof(ClientTransaction));
    }

    if (Volatile.Read(ref _leaseActive) != 0) {
      throw new InvalidOperationException("Release the previous event first.");
    }

    if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0) {
      throw new InvalidOperationException("Another response read is pending.");
    }

    try {
      if (_finalSeen) {
        return null;
      }

      if (_core.Machine.State is not (ClientState.Command or ClientState.AwaitFinal) ||
          _core.IoUncertain) {
        _core.Invalidate();
        throw new ClientException(ClientErrorKind.State);
      }

      var ev = await _core.ReadEventAsync(_deadline, _options, _sensitivity, ReleaseLease,
        cancellationToken).ConfigureAwait(false);
      Volatile.Write(ref _leaseActive, 1);
      if (ev.Kind == ClientEventKind.Finished) {
        _finalSeen = true;
        _remoteCode = ev.RemoteCode;
      }

      return ev;
    }
    finally { Interlocked.Exchange(ref _reading, 0); }
  }

  /// <summary>Completes only after final OK/ERR and release of its event lease.</summary>
  public void Finish() {
    if (Volatile.Read(ref _disposed) != 0) {
      throw new ObjectDisposedException(nameof(ClientTransaction));
    }

    if (Volatile.Read(ref _leaseActive) != 0 ||
        Volatile.Read(ref _reading) != 0) {
      throw new InvalidOperationException("Release the current response event first.");
    }

    if (!_finalSeen) {
      _core.Invalidate();
      throw new ClientException(ClientErrorKind.Incomplete);
    }

    if (_finished) {
      throw new InvalidOperationException("The transaction is already finished.");
    }

    _finished = true;
    Interlocked.Exchange(ref _core.TransactionActive, 0);

    if (_remoteCode.HasValue) {
      AssuanEventDispatch.Report(_core.EventSink, AssuanComponent.Client, AssuanEventId.RemoteFinal,
        AssuanOutcome.RemoteError);
      throw new ClientException(ClientErrorKind.Remote, _remoteCode);
    }
  }

  private void ReleaseLease() {
    _core.Channel.ClearReceivedLeaseLine();
    Volatile.Write(ref _leaseActive, 0);
    _core.Channel.EndReceivedLease();
  }
}
