// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Client;

/// <summary>Category of one borrowed response event.</summary>
public enum ClientEventKind { Data, Status, Comment, End, Inquire, Finished }

/// <summary>One response event borrowing the channel's receive buffer until disposal.</summary>
public sealed class ClientEventLease : IDisposable {
  private readonly ClientInquiry? _inquiry;
  private readonly ReadOnlyMemory<byte> _payload;
  private readonly Action _release;
  private readonly PayloadSensitivity _sensitivity;
  private int _disposed;

  internal ClientEventLease(ClientEventKind kind, ReadOnlyMemory<byte> payload, ReadOnlyMemory<byte> keyword,
  PayloadSensitivity sensitivity, uint? remoteCode, Action release, ClientInquiry? inquiry = null) {
    Kind = kind;
    _payload = payload;
    KeywordBytes = keyword;
    _sensitivity = sensitivity;
    RemoteCode = remoteCode;
    _release = release;
    _inquiry = inquiry;
  }

  /// <summary>Event category without its borrowed content.</summary>
  public ClientEventKind Kind { get; }

  /// <summary>Full ERR code, or null for a successful final or nonfinal event.</summary>
  public uint? RemoteCode { get; }

  /// <summary>Exclusive response guard on an INQUIRE event only.</summary>
  public ClientInquiry Inquiry {
    get {
      CheckOpen();
      return Kind == ClientEventKind.Inquire ? _inquiry! : throw new InvalidOperationException("The event is not an inquiry.");
    }
  }

  /// <summary>Validated ASCII keyword on a status event.</summary>
  public ReadOnlyMemory<byte> KeywordBytes {
    get {
      CheckOpen();
      if (Kind != ClientEventKind.Status) {
        throw new InvalidOperationException("The event has no keyword.");
      }

      return field;
    }
  }

  /// <summary>Borrowed public payload bytes, except for an END event.</summary>
  public ReadOnlyMemory<byte> PayloadBytes {
    get {
      CheckOpen();
      if (Kind is ClientEventKind.End or ClientEventKind.Inquire) {
        throw new InvalidOperationException("This event has no payload.");
      }

      if (_sensitivity == PayloadSensitivity.Secret) {
        throw new InvalidOperationException("Explicit secret exposure required.");
      }

      return _payload;
    }
  }

  /// <summary>Releases this event before the next read or Finish.</summary>
  public void Dispose() {
    if (Interlocked.Exchange(ref _disposed, 1) == 0) {
      if (_inquiry is { IsCompleted: false }) {
        _inquiry.Abort();
      }

      _release();
    }
  }

  /// <summary>Explicitly borrows the payload of a secret-classified event.</summary>
  public ReadOnlyMemory<byte> ExposeSecret() {
    CheckOpen();
    if (_sensitivity != PayloadSensitivity.Secret ||
        Kind is ClientEventKind.End or ClientEventKind.Inquire) {
      throw new InvalidOperationException("No secret payload is available.");
    }

    return _payload;
  }

  /// <inheritdoc />
  public override string ToString()
    => $"ClientEventLease {{ Kind = {Kind}, Length = {_payload.Length}, HasRemoteCode = {RemoteCode.HasValue} }}";

  private void CheckOpen()
    => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
