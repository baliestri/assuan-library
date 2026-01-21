// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections;
using AssuanLibrary.Extensions;

namespace AssuanLibrary;

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

  internal AssuanResponseCollection(byte[] buffer) {
    _buffer = buffer;

    if (buffer.Length == 0) {
      _buffer = [];
      _entries = [];
      return;
    }

    _entries = buffer
      .Split(Characters.LF)
      .Where(entry => entry.All(item => item != Characters.NULL) && entry.Length > 0)
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

  /// <inheritdoc />
  public override string ToString()
    => AssuanDecoder.ToString(_buffer);
}
