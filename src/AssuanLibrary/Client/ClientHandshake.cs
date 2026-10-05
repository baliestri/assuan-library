// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>An owned greeting driven manually without I/O during creation.</summary>
public sealed class ClientHandshake : IAsyncDisposable {
  private readonly ClientCore _core;
  private readonly TransportDeadline _deadline;
  private readonly ClientOptions _options;
  private int _disposed;
  private bool _finalSeen;
  private int _leaseActive;
  private int _reading;
  private uint? _remoteCode;
  private bool _transferred;

  internal ClientHandshake(ClientCore core, ClientOptions options) {
    _core = core;
    _options = options;
    _deadline = new TransportDeadline(options.GreetingTimeout, core.Clock);
  }

  /// <summary>Abandonment closes the owned stream unless ownership was transferred by Finish.</summary>
  public ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0 &&
        !_transferred) {
      _core.Invalidate();
    }

    return ValueTask.CompletedTask;
  }

  /// <summary>Reads one borrowed greeting event or returns null after its final OK/ERR.</summary>
  public async Task<ClientEventLease?> ReadNextAsync(CancellationToken cancellationToken = default) {
    if (Volatile.Read(ref _disposed) != 0) {
      throw new ObjectDisposedException(nameof(ClientHandshake));
    }

    if (Volatile.Read(ref _leaseActive) != 0) {
      throw new InvalidOperationException("Release the previous greeting event first.");
    }

    if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0) {
      throw new InvalidOperationException("Another greeting read is pending.");
    }

    try {
      if (_finalSeen) {
        return null;
      }

      if (_core.Machine.State is not (ClientState.Greeting or ClientState.AwaitFinal) ||
          _core.IoUncertain) {
        _core.Invalidate();
        throw new ClientException(ClientErrorKind.State);
      }

      var ev = await _core.ReadEventAsync(_deadline, _options, PayloadSensitivity.Public, ReleaseLease,
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

  /// <summary>Transfers a ready client only after a final OK and release of its event lease.</summary>
  public AssuanClient Finish() {
    if (Volatile.Read(ref _disposed) != 0) {
      throw new ObjectDisposedException(nameof(ClientHandshake));
    }

    if (Volatile.Read(ref _leaseActive) != 0 ||
        Volatile.Read(ref _reading) != 0) {
      throw new InvalidOperationException("Release the current greeting event first.");
    }

    if (!_finalSeen) {
      _core.Invalidate();
      throw new ClientException(ClientErrorKind.Incomplete);
    }

    if (_remoteCode.HasValue) {
      _core.Invalidate();
      throw new ClientException(ClientErrorKind.GreetingRejected, _remoteCode);
    }

    if (!_core.IsUsable) {
      _core.Invalidate();
      throw new ClientException(ClientErrorKind.State);
    }

    _core.GreetingPending = false;
    _transferred = true;
    Interlocked.Exchange(ref _disposed, 1);
    return AssuanClient.FromReadyCore(_core, _options);
  }

  private void ReleaseLease() {
    _core.Channel.ClearReceivedLeaseLine();
    Volatile.Write(ref _leaseActive, 0);
    _core.Channel.EndReceivedLease();
  }
}
