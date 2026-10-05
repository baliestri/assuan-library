// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Transport;

/// <summary>One owned duplex stream with bounded asynchronous Assuan line framing.</summary>
/// <remarks>
///   One operation may run at a time. Input views remain valid until the next read or close;
///   higher-level sessions must prevent reuse while an event lease is active. Internal buffers
///   are wiped regardless of whether received bytes have been classified as secret.
/// </remarks>
public sealed class AssuanChannel : IAsyncDisposable {
  private readonly byte[] _ahead = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
  private readonly ProtocolLineBuffer _frame = new();
  private readonly Stream _stream;
  private readonly byte[] _write = new byte[ProtocolLineBuffer.MAX_LINE_BYTES];
  private int _active;
  private int _closed;
  private int _pending;
  private int _pendingReadIo;
  private int _pendingWriteIo;

  /// <summary>Takes ownership of a connected duplex byte stream without reading or writing it.</summary>
  public AssuanChannel(Stream stream)
    => _stream = stream ?? throw new ArgumentNullException(nameof(stream));

  /// <summary>Whether bytes for a later line have already been read from the stream.</summary>
  public bool HasBufferedInput => Volatile.Read(ref _closed) == 0 && _pending != 0;

  /// <summary>The last complete line without LF/CRLF; valid until the next read or close.</summary>
  public ReadOnlySpan<byte> Line {
    get {
      CheckOpen();
      return _frame.Line;
    }
  }

  /// <summary>A mutable view of the last complete line for explicit in-place data decoding.</summary>
  public Span<byte> MutableLine {
    get {
      CheckOpen();
      return _frame.MutableLine;
    }
  }

  internal ReadOnlyMemory<byte> ReceivedLineMemory {
    get {
      CheckOpen();
      return _frame.LineMemory;
    }
  }

  /// <summary>Closes the channel and wipes all buffers not still borrowed by pending I/O.</summary>
  public ValueTask DisposeAsync() {
    Close();
    return ValueTask.CompletedTask;
  }

  /// <summary>Reads one line, or returns false for clean EOF exactly at a line boundary.</summary>
  /// <remarks>Timeout, cancellation, framing or I/O failure closes the channel. A custom stream must support cancellation.</remarks>
  public async Task<bool> ReadLineOrEofAsync(TimeSpan timeout, CancellationToken cancellationToken = default) {
    var deadline = CreateDeadline(timeout);
    BeginOperation();
    try {
      _frame.Clear();
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      linked.CancelAfter(deadline.Remaining);
      while (true) {
        CheckOpen();
        if (deadline.Remaining == TimeSpan.Zero) {
          throw new TransportException(TransportErrorKind.Timeout);
        }

        if (_pending != 0) {
          var used = _frame.Feed(_ahead.AsSpan(0, _pending));
          _ahead.AsSpan(used, _pending - used).CopyTo(_ahead);
          _pending -= used;
          _ahead.AsSpan(_pending).Clear();
          if (_frame.IsReady) {
            return true;
          }
        }

        var io = _stream.ReadAsync(_ahead.AsMemory(), linked.Token).AsTask();
        int read;
        try {
          read = await io.WaitAsync(deadline.Remaining, linked.Token).ConfigureAwait(false);
        }
        catch {
          if (!io.IsCompleted) {
            DeferBufferCleanup(io, true);
          }

          throw;
        }

        CheckOpen();
        if (deadline.Remaining == TimeSpan.Zero) {
          throw new TransportException(TransportErrorKind.Timeout);
        }

        if (read == 0) {
          _frame.FinishEof();
          return false;
        }

        _pending = read;
      }
    }
    catch (Exception error) {
      Close();
      throw Classify(error, cancellationToken);
    }
    finally {
      EndOperation();
    }
  }

  /// <summary>Requires a complete line; clean EOF is an error for an active transaction.</summary>
  public async Task ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default) {
    if (!await ReadLineOrEofAsync(timeout, cancellationToken).ConfigureAwait(false)) {
      Close();
      throw new TransportException(TransportErrorKind.Framing, protocolCause: ProtocolErrorKind.UnexpectedEof);
    }
  }

  /// <summary>Writes one already encoded line including LF, then flushes under one total deadline.</summary>
  /// <remarks>The caller owns its input buffer and any OS copies. An uncertain write is never retried.</remarks>
  public async Task WriteLineAsync(ReadOnlyMemory<byte> bytes, TimeSpan timeout,
  CancellationToken cancellationToken = default) {
    var deadline = CreateDeadline(timeout);
    BeginOperation();
    try {
      ValidateWireLine(bytes.Span);
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      linked.CancelAfter(deadline.Remaining);
      bytes.CopyTo(_write);
      var io = _stream.WriteAsync(_write.AsMemory(0, bytes.Length), linked.Token).AsTask();
      try {
        await io.WaitAsync(deadline.Remaining, linked.Token).ConfigureAwait(false);
      }
      catch {
        if (!io.IsCompleted) {
          DeferBufferCleanup(io, false);
        }

        throw;
      }

      CryptographicOperations.ZeroMemory(_write);
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      var flush = _stream.FlushAsync(linked.Token);
      try {
        await flush.WaitAsync(deadline.Remaining, linked.Token).ConfigureAwait(false);
      }
      catch {
        if (!flush.IsCompleted) {
          ObserveLater(flush);
        }

        throw;
      }

      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      CheckOpen();
    }
    catch (Exception error) {
      Close();
      throw Classify(error, cancellationToken);
    }
    finally {
      EndOperation();
    }
  }

  /// <summary>Closes the stream immediately without implicit flush or Assuan shutdown exchange.</summary>
  /// <remarks>Pending noncooperative I/O keeps its borrowed buffer until the underlying task finishes.</remarks>
  public void Close() {
    if (Interlocked.Exchange(ref _closed, 1) == 0) {
      try { _stream.Dispose(); }
      catch (Exception) {
        /* Closing is best-effort; the channel remains unusable. */
      }
    }

    WipeWhenIdle();
  }

  /// <inheritdoc />
  public override string ToString()
    => $"AssuanChannel {{ Closed = {Volatile.Read(ref _closed) != 0} }}";

  private TransportDeadline CreateDeadline(TimeSpan timeout) {
    try { return new TransportDeadline(timeout); }
    catch (TransportException error) when (error.Kind == TransportErrorKind.Timeout) {
      Close();
      throw;
    }
  }

  private void BeginOperation() {
    CheckOpen();
    if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) {
      throw new InvalidOperationException("Another channel operation is in progress.");
    }

    if (Volatile.Read(ref _closed) != 0) {
      EndOperation();
      throw new TransportException(TransportErrorKind.Closed);
    }
  }

  private void EndOperation() {
    Interlocked.Exchange(ref _active, 0);
    WipeWhenIdle();
  }

  private void CheckOpen() {
    if (Volatile.Read(ref _closed) != 0) {
      throw new TransportException(TransportErrorKind.Closed);
    }
  }

  private void WipeWhenIdle() {
    if (Volatile.Read(ref _closed) == 0 ||
        Volatile.Read(ref _active) != 0) {
      return;
    }

    _frame.Dispose();
    _pending = 0;
    if (Volatile.Read(ref _pendingReadIo) == 0) {
      CryptographicOperations.ZeroMemory(_ahead);
    }

    if (Volatile.Read(ref _pendingWriteIo) == 0) {
      CryptographicOperations.ZeroMemory(_write);
    }
  }

  private void DeferBufferCleanup(Task io, bool read) {
    if (read) {
      Volatile.Write(ref _pendingReadIo, 1);
    }
    else {
      Volatile.Write(ref _pendingWriteIo, 1);
    }

    _ = io.ContinueWith(completed => {
      _ = completed.Exception; // Observe a late stream error without formatting it.
      if (read) {
        CryptographicOperations.ZeroMemory(_ahead);
        Volatile.Write(ref _pendingReadIo, 0);
      }
      else {
        CryptographicOperations.ZeroMemory(_write);
        Volatile.Write(ref _pendingWriteIo, 0);
      }

      WipeWhenIdle();
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
  }

  private static void ObserveLater(Task io)
    => _ = io.ContinueWith(completed => {
      _ = completed.Exception;
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

  private static void ValidateWireLine(ReadOnlySpan<byte> bytes) {
    if (bytes.Length > ProtocolLineBuffer.MAX_LINE_BYTES) {
      throw new ProtocolException(ProtocolErrorKind.LineTooLong);
    }

    if (bytes.IsEmpty ||
        bytes[^1] != (byte)'\n') {
      throw new ProtocolException(ProtocolErrorKind.InvalidLine);
    }

    var body = bytes[..^1];
    if (!body.IsEmpty &&
        body[^1] == (byte)'\r') {
      body = body[..^1];
    }

    if (body.IndexOfAny((byte)0, (byte)'\r', (byte)'\n') >= 0) {
      throw new ProtocolException(ProtocolErrorKind.InvalidLine);
    }
  }

  private static TransportException Classify(Exception error, CancellationToken callerToken) => error switch {
    TransportException typed => typed,
    ProtocolException protocol => new TransportException(TransportErrorKind.Framing, protocolCause: protocol.Kind),
    OperationCanceledException => new TransportException(callerToken.IsCancellationRequested
      ? TransportErrorKind.Cancelled
      : TransportErrorKind.Timeout),
    TimeoutException => new TransportException(TransportErrorKind.Timeout),
    ObjectDisposedException => new TransportException(TransportErrorKind.Closed),
    var _ => new TransportException(TransportErrorKind.Io, error)
  };
}
