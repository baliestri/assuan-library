// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Server;

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
    get
    {
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
