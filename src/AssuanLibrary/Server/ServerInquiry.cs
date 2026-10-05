// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>How a peer terminated one inquiry; CAN does not decide the command's final response.</summary>
public enum InquiryOutcome { Completed, Cancelled }

/// <summary>An exclusive borrow of one decoded client data segment.</summary>
public sealed class InquiryChunk : IDisposable {
  private readonly ReadOnlyMemory<byte> _payload;
  private readonly Action _release;
  private readonly PayloadSensitivity _sensitivity;
  private int _disposed;

  internal InquiryChunk(ReadOnlyMemory<byte> payload, PayloadSensitivity sensitivity, Action release) {
    _payload = payload;
    _sensitivity = sensitivity;
    _release = release;
  }

  /// <summary>Access to ordinary public data; secret data requires an explicit Expose call.</summary>
  public ReadOnlyMemory<byte> Data {
    get {
      CheckOpen();
      if (_sensitivity == PayloadSensitivity.Secret) {
        throw new InvalidOperationException("Secret data requires explicit exposure.");
      }

      return _payload;
    }
  }

  /// <summary>Releases the channel buffer before another inquiry read.</summary>
  public void Dispose() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0) {
      _release();
    }
  }

  /// <summary>Explicitly borrows decoded bytes, including classified secret data.</summary>
  public ReadOnlyMemory<byte> Expose() {
    CheckOpen();
    return _payload;
  }

  /// <inheritdoc />
  public override string ToString()
    => $"InquiryChunk {{ Length = {_payload.Length}, Secret = {_sensitivity == PayloadSensitivity.Secret} }}";

  private void CheckOpen()
    => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}

/// <summary>Reads data under one deadline; unfinished disposal invalidates the session.</summary>
public sealed class ServerInquiry : IAsyncDisposable {
  private readonly Action _abort;
  private readonly AssuanChannel _channel;
  private readonly TransportDeadline _deadline;
  private readonly int _limit;
  private readonly ServerMachine _machine;
  private readonly Action _release;
  private readonly PayloadSensitivity _sensitivity;
  private int _chunkActive;
  private bool _finished;
  private InquiryOutcome? _outcome;
  private int _reading;
  private int _received;

  internal ServerInquiry(AssuanChannel channel, ServerMachine machine, TransportDeadline deadline,
  int limit, PayloadSensitivity sensitivity, Action release, Action abort) {
    _channel = channel;
    _machine = machine;
    _deadline = deadline;
    _limit = limit;
    _sensitivity = sensitivity;
    _release = release;
    _abort = abort;
  }

  /// <summary>Closes an unfinished inquiry; it cannot be resumed.</summary>
  public ValueTask DisposeAsync() {
    if (!_finished) {
      _finished = true;
      _abort();
    }

    return ValueTask.CompletedTask;
  }

  /// <summary>Gets the next leased decoded segment, or null after END or CAN.</summary>
  public async Task<InquiryChunk?> ReadNextAsync(CancellationToken cancellationToken = default) {
    if (_finished) {
      throw new ObjectDisposedException(nameof(ServerInquiry));
    }

    if (Volatile.Read(ref _chunkActive) != 0) {
      throw new InvalidOperationException("Release the current data chunk first.");
    }

    if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0) {
      throw new InvalidOperationException("Another inquiry read is pending.");
    }

    try {
      if (_outcome.HasValue) {
        return null;
      }

      while (true) {
        if (_deadline.Remaining == TimeSpan.Zero) {
          throw new ServerException(ServerErrorKind.Timeout);
        }

        await _channel.ReadLineAsync(_deadline.Remaining, cancellationToken).ConfigureAwait(false);
        var line = _channel.Line;
        ProtocolFields.ValidateLine(line);
        if (line.IsEmpty ||
            line[0] == (byte)'#') {
          _machine.ReceiveRequest(line.IsEmpty ? RequestKind.Empty : RequestKind.Comment);
          continue;
        }

        if (line.SequenceEqual("END"u8) ||
            line.SequenceEqual("CAN"u8)) {
          _outcome = line[0] == (byte)'E' ? InquiryOutcome.Completed : InquiryOutcome.Cancelled;
          return null;
        }

        if (!line.StartsWith("D "u8)) {
          throw new ProtocolException(ProtocolErrorKind.InvalidLine);
        }

        _machine.ReceiveRequest(RequestKind.Data);
        var decoded = ProtocolEscaping.DecodeDataInPlace(_channel.MutableLine[2..]);
        if (decoded > (_limit - _received)) {
          throw new ServerException(ServerErrorKind.Limit);
        }

        _received += decoded;
        var payload = _channel.ReceivedLineMemory.Slice(2, decoded);
        _channel.BeginReceivedLease();
        Volatile.Write(ref _chunkActive, 1);
        return new InquiryChunk(payload, _sensitivity, ReleaseChunk);
      }
    }
    catch {
      _abort();
      throw;
    }
    finally { Interlocked.Exchange(ref _reading, 0); }
  }

  /// <summary>Applies a previously observed terminator without draining further bytes.</summary>
  public InquiryOutcome Finish() {
    if (_finished) {
      throw new ObjectDisposedException(nameof(ServerInquiry));
    }

    if (Volatile.Read(ref _chunkActive) != 0 ||
        Volatile.Read(ref _reading) != 0 ||
        !_outcome.HasValue) {
      throw new InvalidOperationException("Observe END or CAN and release the active chunk before finishing.");
    }

    try {
      _machine.ReceiveRequest(_outcome == InquiryOutcome.Completed ? RequestKind.End : RequestKind.Can);
      _finished = true;
      _release();
      return _outcome.Value;
    }
    catch {
      _abort();
      throw;
    }
  }

  private void ReleaseChunk() {
    // The channel may have closed while this chunk was borrowed; defer all
    // framed-buffer disposal until the last lease has released it.
    _channel.ClearReceivedLeaseLine();
    Volatile.Write(ref _chunkActive, 0);
    _channel.EndReceivedLease();
  }
}
