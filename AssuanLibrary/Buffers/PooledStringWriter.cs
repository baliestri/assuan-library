// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using CharPool = System.Buffers.ArrayPool<char>;

namespace AssuanLibrary.Buffers;

/// <summary>
///   A high-performance buffer writer for characters that utilizes array pooling.
/// </summary>
/// <param name="initialCapacity">The initial capacity of the buffer.</param>
public struct PooledStringWriter(int initialCapacity) : IBufferWriter<char>, IDisposable {
  private char[] _buffer = CharPool.Shared.Rent(initialCapacity);
  private int _written;
  private bool _disposed;

  /// <inheritdoc />
  public void Advance(int count) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));
    ArgumentOutOfRangeException.ThrowIfNegative(count);

    _written += count;
  }

  /// <inheritdoc />
  public Memory<char> GetMemory(int sizeHint = 0) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));
    EnsureCapacity(sizeHint);

    return _buffer.AsMemory(_written);
  }

  /// <inheritdoc />
  public Span<char> GetSpan(int sizeHint = 0) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));
    EnsureCapacity(sizeHint);

    return _buffer.AsSpan(_written);
  }

  /// <summary>
  ///   Writes a single character to the buffer.
  /// </summary>
  /// <param name="value">The character to write.</param>
  public void Write(char value) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));
    EnsureCapacity(1);

    _buffer[_written++] = value;
  }

  /// <summary>
  ///   Writes a span of characters to the buffer.
  /// </summary>
  /// <param name="value">The span of characters to write.</param>
  public void Write(ReadOnlySpan<char> value) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));
    EnsureCapacity(value.Length);

    Array.Copy(value.ToArray(), 0, _buffer, _written, value.Length);
    _written += value.Length;
  }

  /// <summary>
  ///   Writes a string to the buffer.
  /// </summary>
  /// <param name="value">The string to write.</param>
  public void Write(string? value) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));

    if (string.IsNullOrEmpty(value)) {
      return;
    }

    EnsureCapacity(value!.Length);
    Array.Copy(value.ToCharArray(), 0, _buffer, _written, value.Length);
    _written += value.Length;
  }

  /// <summary>
  ///   Gets the written data as a string and disposes the writer.
  /// </summary>
  /// <returns>A string containing the written data.</returns>
  public override string ToString() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));

    var buffer = _buffer.AsSpan(0, _written).ToArray();
    var str = new string(buffer);
    Dispose();
    return str;
  }

  /// <summary>
  ///   Gets the written data as a string with a maximum length and disposes the writer.
  /// </summary>
  /// <param name="maxLength">The maximum length of the string to return.</param>
  /// <returns>A string containing the written data.</returns>
  public string ToString(int maxLength) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringWriter));

    var length = Math.Min(_written, maxLength);
    var buffer = _buffer.AsSpan(0, length).ToArray();
    var str = new string(buffer);
    Dispose();
    return str;
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    CharPool.Shared.Return(_buffer, true);
    _buffer = [];
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
    var newBuffer = CharPool.Shared.Rent(newSize);

    _buffer.AsSpan(0, _written).CopyTo(newBuffer);
    CharPool.Shared.Return(_buffer, true);

    _buffer = newBuffer;
  }
}
