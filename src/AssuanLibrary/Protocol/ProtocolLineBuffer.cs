// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;

namespace AssuanLibrary.Protocol;

/// <summary>Fixed-capacity incremental framing for one Assuan wire line.</summary>
/// <remarks>Failure is sticky until <see cref="Clear" />. A connection owner must close after framing failure.</remarks>
public sealed class ProtocolLineBuffer : IDisposable {
  /// <summary>Maximum wire line size including the LF or CRLF terminator.</summary>
  public const int MAX_LINE_BYTES = ProtocolEncoder.MAX_LINE_BYTES;

  private readonly byte[] _storage = new byte[MAX_LINE_BYTES];
  private int _contentLength;
  private bool _disposed;
  private bool _failed;
  private int _length;
  private bool _ready;

  /// <summary>Whether a complete line is available, including an empty line.</summary>
  public bool IsReady {
    get {
      CheckDisposed();
      return _ready;
    }
  }

  /// <summary>A complete line without LF and its optional preceding CR, or empty before completion.</summary>
  /// <remarks>Do not retain across <see cref="Clear" />, another feed, or disposal.</remarks>
  public ReadOnlySpan<byte> Line {
    get {
      CheckDisposed();
      return _ready ? _storage.AsSpan(0, _contentLength) : ReadOnlySpan<byte>.Empty;
    }
  }

  /// <summary>A mutable view of a complete line for in-place data decoding.</summary>
  /// <remarks>Do not retain across <see cref="Clear" /> or disposal; the caller owns any resulting copies.</remarks>
  public Span<byte> MutableLine {
    get {
      CheckDisposed();
      return _ready ? _storage.AsSpan(0, _contentLength) : Span<byte>.Empty;
    }
  }

  internal ReadOnlyMemory<byte> LineMemory {
    get {
      CheckDisposed();
      return _ready ? _storage.AsMemory(0, _contentLength) : ReadOnlyMemory<byte>.Empty;
    }
  }

  /// <summary>Wipes the entire managed allocation and prevents future operations.</summary>
  public void Dispose() {
    if (_disposed) {
      return;
    }

    CryptographicOperations.ZeroMemory(_storage);
    _length = 0;
    _contentLength = 0;
    _ready = false;
    _disposed = true;
  }

  /// <summary>Consumes bytes through the first LF; subsequent bytes stay with the caller.</summary>
  /// <returns>Number of input bytes consumed, zero if a line is already ready.</returns>
  public int Feed(ReadOnlySpan<byte> input) {
    CheckDisposed();
    if (_failed) {
      throw new ProtocolException(ProtocolErrorKind.LineTooLong);
    }

    if (_ready) {
      return 0;
    }

    for (var index = 0; index < input.Length; ++index) {
      var value = input[index];
      _storage[_length++] = value;
      if (value == (byte)'\n') {
        _contentLength = _length - 1;
        if (_contentLength != 0 &&
            _storage[_contentLength - 1] == (byte)'\r') {
          --_contentLength;
        }

        _ready = true;
        return index + 1;
      }

      if (_length == MAX_LINE_BYTES) {
        Clear();
        _failed = true;
        throw new ProtocolException(ProtocolErrorKind.LineTooLong);
      }
    }

    return input.Length;
  }

  /// <summary>Checks whether the stream ended at a line boundary, not whether the session succeeded.</summary>
  public void FinishEof() {
    CheckDisposed();
    if (_failed) {
      throw new ProtocolException(ProtocolErrorKind.LineTooLong);
    }

    if (_length != 0 &&
        !_ready) {
      throw new ProtocolException(ProtocolErrorKind.UnexpectedEof);
    }
  }

  /// <summary>Wipes the entire allocation and resets local framing state for another line.</summary>
  public void Clear() {
    CheckDisposed();
    CryptographicOperations.ZeroMemory(_storage);
    _length = 0;
    _contentLength = 0;
    _ready = false;
    _failed = false;
  }

  /// <inheritdoc />
  public override string ToString()
    => $"ProtocolLineBuffer {{ Ready = {_ready}, Failed = {_failed}, Closed = {_disposed} }}";

  private void CheckDisposed()
    => ObjectDisposedException.ThrowIf(_disposed, this);
}
