// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Expressions;

/// <summary>
///   Represents a S-Expression (Symbolic Expression).
/// </summary>
public abstract class SymbolicExpression : IEquatable<SymbolicExpression> {
  /// <summary>
  ///   The type of the symbolic expression.
  /// </summary>
  public abstract SymbolicExpressionType Type { get; }

  /// <inheritdoc />
  public bool Equals(SymbolicExpression? other) {
    if (other is null) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    return Type == other.Type;
  }

  /// <inheritdoc />
  public override bool Equals(object? obj) {
    if (obj is null) {
      return false;
    }

    if (ReferenceEquals(this, obj)) {
      return true;
    }

    return obj.GetType() == GetType() &&
           Equals((SymbolicExpression)obj);
  }

  /// <inheritdoc />
  public override int GetHashCode()
    => (int)Type;

  public static bool operator ==(SymbolicExpression? left, SymbolicExpression? right)
    => Equals(left, right);

  public static bool operator !=(SymbolicExpression? left, SymbolicExpression? right)
    => !Equals(left, right);
}
