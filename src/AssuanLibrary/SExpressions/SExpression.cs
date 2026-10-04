// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

/// <summary>An independently owned immutable S-expression of binary atoms or nested lists.</summary>
/// <remarks>Owned atoms use ordinary managed memory without automatic secret erasure.</remarks>
public sealed class SExpression {
  private readonly byte[]? _atom;
  private readonly IReadOnlyList<SExpression>? _children;

  private SExpression(byte[] atom)
    => _atom = atom;

  private SExpression(SExpression[] children)
    => _children = Array.AsReadOnly(children);

  /// <summary>Whether this expression is an atom rather than a list.</summary>
  public bool IsAtom => _atom is not null;

  /// <summary>The atom's binary bytes, or an error for lists.</summary>
  public ReadOnlySpan<byte> AtomBytes => _atom ??
                                         throw new InvalidOperationException("A list has no atom bytes.");

  /// <summary>Read-only children, or an error for atoms.</summary>
  public IReadOnlyList<SExpression> Children => _children ??
                                                throw new InvalidOperationException("An atom has no children.");

  /// <summary>Creates an atom by copying the supplied binary bytes.</summary>
  public static SExpression Atom(ReadOnlySpan<byte> bytes) => new(bytes.ToArray());

  /// <summary>Creates a list by copying the child collection; children are immutable values.</summary>
  /// <exception cref="ArgumentException">A child is null.</exception>
  public static SExpression List(IEnumerable<SExpression> children) {
    ArgumentNullException.ThrowIfNull(children);
    var values = new List<SExpression>();
    foreach (var child in children) {
      if (child is null) {
        throw new ArgumentException("A list child cannot be null.", nameof(children));
      }

      values.Add(child);
    }

    return FromChildren(values);
  }

  internal static SExpression FromChildren(List<SExpression> children)
    => new(children.ToArray());

  /// <inheritdoc />
  public override string ToString()
    => IsAtom ? $"Atom {{ Length = {_atom!.Length} }}" : $"List {{ Children = {_children!.Count} }}";
}
