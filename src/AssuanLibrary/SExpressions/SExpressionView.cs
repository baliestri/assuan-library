// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>A scoped view of one validated canonical expression; its atoms borrow the caller's bytes.</summary>
/// <remarks>Do not modify or reuse the source buffer until the view is no longer in use.</remarks>
public readonly ref struct SExpressionView {
  private readonly ReadOnlySpan<byte> _bytes;

  internal SExpressionView(ReadOnlySpan<byte> bytes)
    => _bytes = bytes;

  /// <summary>Whether this expression is an atom rather than a list.</summary>
  public bool IsAtom => !_bytes.IsEmpty ? _bytes[0] != (byte)'(' : throw new InvalidOperationException("No expression is present.");

  /// <summary>The borrowed binary atom bytes; never decoded as text implicitly.</summary>
  /// <exception cref="InvalidOperationException">The expression is a list or is uninitialized.</exception>
  public ReadOnlySpan<byte> AtomBytes {
    get {
      if (!IsAtom) {
        throw new InvalidOperationException("A list has no atom bytes.");
      }

      var separator = _bytes.IndexOf((byte)':');
      return _bytes[(separator + 1)..];
    }
  }

  /// <summary>Sequential, allocation-free access to a list's children.</summary>
  /// <exception cref="InvalidOperationException">The expression is an atom or is uninitialized.</exception>
  public SExpressionChildren Children => !IsAtom
    ? new SExpressionChildren(_bytes)
    : throw new InvalidOperationException("An atom has no children.");

  internal ReadOnlySpan<byte> CanonicalBytes => _bytes;

  /// <summary>Copies the atom bytes and list structure into an independently owned expression.</summary>
  /// <remarks>The returned bytes use ordinary managed memory, not secret-wiping storage.</remarks>
  public SExpression ToOwned() {
    if (IsAtom) {
      return SExpression.Atom(AtomBytes);
    }

    var parents = new Stack<List<SExpression>>();
    var position = 0;
    while (position < _bytes.Length) {
      var current = _bytes[position];
      switch (current) {
        case (byte)'(':
          parents.Push([]);
          ++position;
          break;
        case (byte)')': {
          var completed = SExpression.FromChildren(parents.Pop());
          ++position;
          if (parents.Count == 0) {
            return completed;
          }

          parents.Peek().Add(completed);
          break;
        }
        default: {
          var length = 0;
          while (_bytes[position] != (byte)':') {
            length = ((length * 10) + _bytes[position++]) - (byte)'0';
          }

          ++position;
          parents.Peek().Add(SExpression.Atom(_bytes.Slice(position, length)));
          position += length;
          break;
        }
      }
    }

    throw new InvalidOperationException("The previously validated expression was modified.");
  }

  /// <inheritdoc />
  public override string ToString()
    => IsAtom ? $"Atom {{ Length = {AtomBytes.Length} }}" : "List { .. }";
}

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
      if (_position >= (_list.Length - 1)) {
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
