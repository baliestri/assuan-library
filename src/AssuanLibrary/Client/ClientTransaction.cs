// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>Exclusive command response stream, unfinished disposal closes the connection.</summary>
public sealed class ClientTransaction : IAsyncDisposable {
  private readonly ClientCore _core;
  private readonly TransportDeadline _deadline;
  private readonly PayloadSensitivity _sensitivity;
  private int _disposed;
  private bool _finalSeen;
  private bool _finished;
  private int _leaseActive;
  private int _reading;
  private uint? _remoteCode;

  internal ClientTransaction(ClientCore core, TransportDeadline deadline, PayloadSensitivity sensitivity) {
    _core = core;
    _deadline = deadline;
    _sensitivity = sensitivity;
  }

  /// <summary>Abandoning a transaction without a final response makes the client unusable.</summary>
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

      if (_core.Machine.State != ClientState.Command ||
          _core.IoUncertain) {
        throw new ClientException(ClientErrorKind.State);
      }

      while (true) {
        if (_deadline.Remaining == TimeSpan.Zero) {
          throw new ClientException(ClientErrorKind.Timeout);
        }

        _core.IoUncertain = true;
        await _core.Channel.ReadLineAsync(_deadline.Remaining, cancellationToken).ConfigureAwait(false);
        ProtocolResponseKind wireKind;
        int payloadOffset = 0, payloadLength = 0, keywordLength = 0;
        uint? remoteCode = null;
        {
          var line = _core.Channel.Line;
          var parsed = ProtocolResponse.Parse(line);
          wireKind = parsed.Kind;
          switch (wireKind) {
            case ProtocolResponseKind.Data or ProtocolResponseKind.Comment or ProtocolResponseKind.Ok or ProtocolResponseKind.Err:
              payloadLength = parsed.PayloadBytes.Length;
              payloadOffset = wireKind switch {
                ProtocolResponseKind.Data => 2,
                ProtocolResponseKind.Comment => 1,
                var _ => line.Length - payloadLength
              };
              break;
            case ProtocolResponseKind.Status:
              keywordLength = parsed.KeywordBytes.Length;
              payloadLength = parsed.Arguments.Length;
              payloadOffset = line.Length - payloadLength;
              break;
          }

          if (wireKind == ProtocolResponseKind.Err) {
            remoteCode = parsed.ErrorCode;
          }
        }

        switch (wireKind) {
          case ProtocolResponseKind.Empty:
            _core.IoUncertain = false;
            continue;
          case ProtocolResponseKind.Inquire:
            throw new ClientException(ClientErrorKind.UnsupportedInquiry);
        }

        if (cancellationToken.IsCancellationRequested) {
          throw new ClientException(ClientErrorKind.Cancelled);
        }

        if (_deadline.Remaining == TimeSpan.Zero) {
          throw new ClientException(ClientErrorKind.Timeout);
        }

        _core.Machine.Receive(wireKind);
        var kind = wireKind switch {
          ProtocolResponseKind.Data => ClientEventKind.Data,
          ProtocolResponseKind.Status => ClientEventKind.Status,
          ProtocolResponseKind.Comment => ClientEventKind.Comment,
          ProtocolResponseKind.End => ClientEventKind.End,
          ProtocolResponseKind.Ok or ProtocolResponseKind.Err => ClientEventKind.Finished,
          var _ => throw new ClientException(ClientErrorKind.Protocol)
        };

        if (kind == ClientEventKind.Data) {
          payloadLength = ProtocolEscaping.DecodeDataInPlace(_core.Channel.MutableLine[2..]);
        }

        var received = _core.Channel.ReceivedLineMemory;
        var payload = received.Slice(payloadOffset, payloadLength);
        var keyword = kind == ClientEventKind.Status ? received.Slice(2, keywordLength) : ReadOnlyMemory<byte>.Empty;
        _core.Channel.BeginReceivedLease();
        Volatile.Write(ref _leaseActive, 1);
        if (kind == ClientEventKind.Finished) {
          _finalSeen = true;
          _remoteCode = remoteCode;
        }

        _core.IoUncertain = false;
        return new ClientEventLease(kind, payload, keyword, _sensitivity, remoteCode, ReleaseLease);
      }
    }
    catch (Exception error) {
      _core.Invalidate();
      throw ClientCore.Classify(error, cancellationToken);
    }
    finally { Interlocked.Exchange(ref _reading, 0); }
  }

  /// <summary>Completes only after a final OK/ERR and the event lease has been released.</summary>
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
      throw new ClientException(ClientErrorKind.Remote, _remoteCode);
    }
  }

  private void ReleaseLease() {
    _core.Channel.ClearReceivedLeaseLine();
    Volatile.Write(ref _leaseActive, 0);
    _core.Channel.EndReceivedLease();
  }
}
