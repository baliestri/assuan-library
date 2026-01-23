// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Expressions;

/// <summary>
///   Represents a collection of symbolic expressions.
/// </summary>
/// <param name="children">The child symbolic expressions.</param>
public sealed class SymbolicExpressionCollection(IReadOnlyCollection<SymbolicExpression> children) : SymbolicExpression {
  /// <inheritdoc />
  public override SymbolicExpressionType Type => SymbolicExpressionType.Collection;

  /// <summary>
  ///   The child symbolic expressions.
  /// </summary>
  public IReadOnlyCollection<SymbolicExpression> Children { get; } = children;

  /// <inheritdoc />
  public override string ToString()
    => $"Collection[{Children.Count} children]";
}
