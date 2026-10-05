// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>Exclusive, bounded permission to answer one server INQUIRE.</summary>
public sealed class ClientInquiry : IAsyncDisposable {
  private readonly ClientCore _core;
  private readonly TransportDeadline _deadline;
  private readonly int _keywordLength;
  private readonly int _maxBytes;
  private readonly SecretBuffer _metadata = new(1000);
  private readonly byte[] _scratch = new byte[1000];
  private readonly PayloadSensitivity _sensitivity;
  private int _active;
  private int _disposed;
  private int _sent;

  internal ClientInquiry(ClientCore core, TransportDeadline parentDeadline, ClientOptions options,
  PayloadSensitivity sensitivity, ReadOnlySpan<byte> keyword, ReadOnlySpan<byte> arguments) {
    _core = core;
    _sensitivity = sensitivity;
    _keywordLength = keyword.Length;
    _metadata.Append(keyword);
    _metadata.Append(arguments);
    _maxBytes = options.MaxInquiryBytes;
    var remaining = TimeSpan.FromTicks(Math.Min(parentDeadline.Remaining.Ticks, options.InquiryTimeout.Ticks));
    if (remaining == TimeSpan.Zero) {
      Abort();
      throw new ClientException(ClientErrorKind.Timeout);
    }

    _deadline = new TransportDeadline(remaining, core.Clock);
  }

  /// <summary>Validated ASCII inquiry keyword copied into owned protected storage.</summary>
  public ReadOnlyMemory<byte> KeywordBytes => _metadata.ExposeMemoryForIo()[.._keywordLength];

  /// <summary>Literal public inquiry arguments copied once; secret arguments require explicit exposure.</summary>
  public ReadOnlyMemory<byte> Arguments => _sensitivity == PayloadSensitivity.Public
    ? _metadata.ExposeMemoryForIo()[_keywordLength..]
    : throw new InvalidOperationException("Explicit secret exposure required.");

  /// <summary>Whether END or CAN have already been written successfully.</summary>
  public bool IsCompleted { get; private set; }

  internal TransportDeadline Deadline => _deadline;

  /// <summary>Abandoning an unfinished inquiry invalidates the client and closes its stream.</summary>
  public ValueTask DisposeAsync() {
    if (!IsCompleted) {
      Abort();
    }

    return ValueTask.CompletedTask;
  }

  /// <summary>Explicitly borrows secret-classified inquiry arguments.</summary>
  public ReadOnlyMemory<byte> ExposeSecretArguments()
    => _sensitivity == PayloadSensitivity.Secret
      ? _metadata.ExposeMemoryForIo()[_keywordLength..]
      : throw new InvalidOperationException("No secret arguments are available.");

  /// <summary>Writes escaped public bytes in bounded D lines, including a D line for empty input.</summary>
  public ValueTask SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    => SendAsync(data, cancellationToken);

  /// <summary>Writes protected caller-owned bytes without taking ownership of the secret buffer.</summary>
  public ValueTask SendSecretAsync(SecretBuffer data, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(data);

    return SendAsync(data.ExposeMemoryForIo(), cancellationToken);
  }

  private async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken token) {
    Begin();
    try {
      if (data.Length > (_maxBytes - _sent)) {
        Abort();
        throw new ClientException(ClientErrorKind.Limit);
      }

      var total = _sent + data.Length;
      do {
        var (used, written) = ProtocolEncoder.EncodeDataChunk(data.Span, _scratch);
        await WriteAsync(_scratch.AsMemory(0, written), token).ConfigureAwait(false);
        CryptographicOperations.ZeroMemory(_scratch);
        data = data[used..];
      } while (!data.IsEmpty);

      _sent = total;
    }
    catch (Exception error) {
      Abort();
      throw ClientCore.Classify(error, token);
    }
    finally {
      CryptographicOperations.ZeroMemory(_scratch);
      End();
    }
  }

  /// <summary>Writes END and returns control to the enclosing command or greeting.</summary>
  public Task FinishAsync(CancellationToken cancellationToken = default)
    => CompleteAsync(false, cancellationToken);

  /// <summary>Writes CAN; the enclosing operation must still read its final OK or ERR.</summary>
  public Task CancelAsync(CancellationToken cancellationToken = default)
    => CompleteAsync(true, cancellationToken);

  private async Task CompleteAsync(bool cancelled, CancellationToken token) {
    Begin();
    try {
      await WriteAsync(cancelled ? "CAN\n"u8.ToArray() : "END\n"u8.ToArray(), token).ConfigureAwait(false);
      _core.Machine.FinishInquiry(cancelled);
      IsCompleted = true;
      _metadata.Dispose();
    }
    catch (Exception error) {
      Abort();
      throw ClientCore.Classify(error, token);
    }
    finally { End(); }
  }

  /// <inheritdoc />
  public override string ToString()
    => $"ClientInquiry {{ KeywordLength = {_keywordLength}, Sent = {_sent}, Completed = {IsCompleted} }}";

  internal void Abort() {
    Interlocked.Exchange(ref _disposed, 1);
    _metadata.Dispose();
    CryptographicOperations.ZeroMemory(_scratch);
    _core.Invalidate();
  }

  private void Begin() {
    if (IsCompleted ||
        Volatile.Read(ref _disposed) != 0 ||
        _core.Machine.State != ClientState.Inquiry) {
      throw new ClientException(ClientErrorKind.State);
    }

    if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) {
      throw new InvalidOperationException("An inquiry operation is already pending.");
    }

    if (Volatile.Read(ref _disposed) != 0) {
      End();
      throw new ClientException(ClientErrorKind.State);
    }
  }

  private void End()
    => Interlocked.Exchange(ref _active, 0);

  private async Task WriteAsync(ReadOnlyMemory<byte> wire, CancellationToken token) {
    if (_deadline.Remaining == TimeSpan.Zero) {
      throw new ClientException(ClientErrorKind.Timeout);
    }

    _core.IoUncertain = true;
    await _core.Channel.WriteLineAsync(wire, _deadline.Remaining, token).ConfigureAwait(false);

    if (_deadline.Remaining == TimeSpan.Zero) {
      throw new ClientException(ClientErrorKind.Timeout);
    }

    _core.IoUncertain = false;
  }
}
