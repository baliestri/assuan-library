// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections;
using AssuanLibrary.Extensions;
using AssuanLibrary.Protocol.Buffers;

namespace AssuanLibrary.Protocol;

/// <summary>
///   Represents a collection of responses from the Assuan protocol (aka multi-line response).
/// </summary>
public sealed class AssuanResponseCollection : IReadOnlyList<AssuanResponse> {
  private readonly byte[] _buffer;
  private readonly AssuanResponse[] _entries;

  internal AssuanResponseCollection() {
    _buffer = [];
    _entries = [];
  }

  internal AssuanResponseCollection(ReadOnlyMemory<byte> memory) {
    if (memory.Length == 0) {
      _buffer = [];
      _entries = [];
      return;
    }

    _buffer = memory.ToArray();
    _entries = _buffer
      .Split(Characters.LINE_FEED)
      .Select(entry => new AssuanResponse(entry.ToArray()))
      .ToArray();
  }

  internal AssuanResponseCollection(byte[] buffer) {
    if (buffer.Length == 0) {
      _buffer = [];
      _entries = [];
      return;
    }

    _buffer = buffer;
    _entries = buffer
      .Split(Characters.LINE_FEED)
      .Select(entry => new AssuanResponse(entry))
      .ToArray();
  }

  /// <inheritdoc />
  public AssuanResponse this[int index] => _entries[index];

  /// <inheritdoc />
  public int Count => _entries.Length;

  /// <inheritdoc />
  public IEnumerator<AssuanResponse> GetEnumerator()
    => ((IEnumerable<AssuanResponse>)_entries).GetEnumerator();

  /// <inheritdoc />
  IEnumerator IEnumerable.GetEnumerator()
    => GetEnumerator();

  /// <summary>
  ///   Gets the original buffer representing the entire response collection.
  /// </summary>
  /// <returns>The original byte array buffer.</returns>
  public byte[] GetOriginalBuffer()
    => _buffer;

  /// <inheritdoc />
  public override string ToString()
    => AssuanDecoder.ToString(_buffer);

  public static AssuanResponseCollection Create(params AssuanResponse[] responses) {
    if (responses.Length == 0) {
      return new AssuanResponseCollection();
    }

    using var writer = new PooledByteWriter(responses.Sum(ar => ar.Length) + responses.Length);

    foreach (var response in responses) {
      var originalBuffer = response.GetOriginalBuffer();
      writer.Write(originalBuffer);

      if (!originalBuffer.EndsWith(Characters.LINE_FEED)) {
        writer.Write(Characters.LINE_FEED);
      }
    }

    return new AssuanResponseCollection(writer.ToArray());
  }
}
