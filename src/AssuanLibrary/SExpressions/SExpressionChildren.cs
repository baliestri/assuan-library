// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>Scoped sequential children of a validated canonical list.</summary>
public readonly ref struct SExpressionChildren {
  private readonly ReadOnlySpan<byte> _list;

  internal SExpressionChildren(ReadOnlySpan<byte> list) => _list = list;

  /// <summary>Creates an enumerator without copying atom data or allocating indexes.</summary>
  public Enumerator GetEnumerator()
    => new(_list);

  /// <summary>Iterates over the immediate children of a list.</summary>
  public ref struct Enumerator {
    private readonly ReadOnlySpan<byte> _list;
    private int _position;
    private SExpressionView _current;
    private bool _hasCurrent;

    internal Enumerator(ReadOnlySpan<byte> list) {
      _list = list;
      _position = 1;
      _current = default;
      _hasCurrent = false;
    }

    /// <summary>The current child, borrowing the original validated input.</summary>
    public readonly SExpressionView Current => _hasCurrent
      ? _current
      : throw new InvalidOperationException("There is no current list child.");

    /// <summary>Advances to the next immediate child.</summary>
    public bool MoveNext() {
      if (_position >= _list.Length - 1) {
        _hasCurrent = false;
        return false;
      }

      var next = SExpressionScanner.SkipValidated(_list, _position);
      _current = new SExpressionView(_list[_position..next]);
      _position = next;
      _hasCurrent = true;
      return true;
    }
  }
}
