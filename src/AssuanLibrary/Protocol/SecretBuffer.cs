// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;

namespace AssuanLibrary.Protocol;

/// <summary>Fixed-capacity, pinned managed secret bytes, wiped explicitly on clear or disposal.</summary>
/// <remarks>
///   Does not erase the caller's source, runtime/OS copies, swap, or crash dumps. Use <see langword="using" /> for
///   deterministic cleanup.
/// </remarks>
public sealed class SecretBuffer : IDisposable {
  private readonly byte[] _storage;
  private int _disposed;
  private int _length;

  /// <summary>Allocates exactly the specified number of initially zero bytes, without storing a secret yet.</summary>
  public SecretBuffer(int capacity) {
    ArgumentOutOfRangeException.ThrowIfNegative(capacity);
    // Pinned storage is never moved by the collector, so wiping it leaves no relocated copy behind.
    _storage = GC.AllocateArray<byte>(capacity, true);
  }

  /// <summary>Number of initialized bytes.</summary>
  public int Length {
    get
    {
      CheckDisposed();
      return _length;
    }
  }

  /// <summary>The fixed maximum number of bytes this buffer can hold.</summary>
  public int Capacity {
    get
    {
      CheckDisposed();
      return _storage.Length;
    }
  }

  /// <summary>Wipes the entire allocation and prevents further use.</summary>
  public void Dispose() {
    if (Interlocked.Exchange(ref _disposed, 1) != 0) {
      return;
    }

    CryptographicOperations.ZeroMemory(_storage);
    _length = 0;
  }

  /// <summary>Appends bytes without reallocating; a failed append does not modify the buffer.</summary>
  /// <exception cref="LimitException">The new bytes exceed the fixed capacity.</exception>
  public void Append(ReadOnlySpan<byte> bytes) {
    CheckDisposed();
    if (bytes.Length > _storage.Length - _length) {
      throw new LimitException(LimitErrorKind.CapacityExceeded);
    }

    bytes.CopyTo(_storage.AsSpan(_length));
    _length += bytes.Length;
  }

  /// <summary>Explicitly exposes initialized bytes, without copying or transferring ownership.</summary>
  /// <remarks>The caller must not retain this view across clear, append or disposal.</remarks>
  public ReadOnlySpan<byte> Expose() {
    CheckDisposed();
    return _storage.AsSpan(0, _length);
  }

  // Only library code may borrow heap-addressable memory during an awaited I/O operation.
  // Its caller must keep the buffer alive and undisposed until that operation completes.
  internal ReadOnlyMemory<byte> ExposeMemoryForIo() {
    CheckDisposed();
    return _storage.AsMemory(0, _length);
  }

  /// <summary>Wipes the entire allocation, resets length and retains capacity for reuse.</summary>
  public void Clear() {
    CheckDisposed();
    CryptographicOperations.ZeroMemory(_storage);
    _length = 0;
  }

  /// <inheritdoc />
  public override string ToString()
    => $"SecretBuffer {{ Length = {_length}, Capacity = {_storage.Length}, Closed = {Volatile.Read(ref _disposed) != 0} }}";

  private void CheckDisposed()
    => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
