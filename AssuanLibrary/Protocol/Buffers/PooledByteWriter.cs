// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using BytePool = System.Buffers.ArrayPool<byte>;

namespace AssuanLibrary.Protocol.Buffers;

/// <summary>
///   A high-performance buffer writer for bytes that utilizes array pooling.
/// </summary>
/// <param name="initialCapacity">The initial capacity of the buffer.</param>
public struct PooledByteWriter(int initialCapacity) : IBufferWriter<byte>, IDisposable {
  private byte[] _buffer = BytePool.Shared.Rent(initialCapacity);
  private int _written = 0;
  private bool _disposed;

  /// <inheritdoc />
  public void Advance(int count) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledByteWriter));
    ArgumentOutOfRangeException.ThrowIfNegative(count);

    _written += count;
  }

  /// <inheritdoc />
  public Memory<byte> GetMemory(int sizeHint = 0) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledByteWriter));
    EnsureCapacity(sizeHint);

    return _buffer.AsMemory(_written);
  }

  /// <inheritdoc />
  public Span<byte> GetSpan(int sizeHint = 0) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledByteWriter));
    EnsureCapacity(sizeHint);

    return _buffer.AsSpan(_written);
  }

  /// <summary>
  ///   Writes a single byte to the buffer.
  /// </summary>
  /// <param name="value">The byte to write.</param>
  public void Write(byte value) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledByteWriter));
    EnsureCapacity(1);

    _buffer[_written++] = value;
  }

  /// <summary>
  ///   Writes a byte array to the buffer.
  /// </summary>
  /// <param name="source">The byte array to write.</param>
  public void Write(byte[] source) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledByteWriter));
    ArgumentNullException.ThrowIfNull(source);
    EnsureCapacity(source.Length);

    Array.Copy(source, 0, _buffer, _written, source.Length);
    _written += source.Length;
  }

  /// <summary>
  ///   Gets the written data as a read-only memory segment.
  /// </summary>
  /// <returns>A <see cref="ReadOnlyMemory{T}" /> containing the written data.</returns>
  public ReadOnlyMemory<byte> ToReadOnlyMemory() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledByteWriter));

    return _buffer.AsSpan(0, _written).ToArray();
  }

  /// <summary>
  ///   Gets the written data as a byte array.
  /// </summary>
  /// <returns>A byte array containing the written data.</returns>
  public byte[] ToArray() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledByteWriter));

    return _buffer.AsSpan(0, _written).ToArray();
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    BytePool.Shared.Return(_buffer, true);
    _buffer = null!;
    _written = 0;
    _disposed = true;
  }

  private void EnsureCapacity(int sizeHint) {
    ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

    var requiredSize = _written + Math.Max(sizeHint, 1);

    if (requiredSize <= _buffer.Length) {
      return;
    }

    var newSize = Math.Max(requiredSize, _buffer.Length * 2);
    var newBuffer = BytePool.Shared.Rent(newSize);

    _buffer.AsSpan(0, _written).CopyTo(newBuffer);
    BytePool.Shared.Return(_buffer, true);

    _buffer = newBuffer;
  }
}
