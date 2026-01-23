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
    if (ReferenceEquals(this, other)) {
      return true;
    }

    if (other is null ||
        Type != other.Type) {
      return false;
    }

    return InheritorEquals(other);
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is SymbolicExpression other && Equals(other);

  /// <inheritdoc />
  public override int GetHashCode() {
    var typeHashCode = Type.GetHashCode();
    var inheritorHashCode = InheritorGetHashCode();

    return typeHashCode ^ inheritorHashCode;
  }

  /// <summary>
  ///   Determines whether the inheritor instances are equal.
  /// </summary>
  /// <param name="other">The other instance.</param>
  /// <returns><see langword="true" /> if the two instances are equal; otherwise, <see langword="false" />.</returns>
  protected abstract bool InheritorEquals(SymbolicExpression other);

  /// <summary>
  ///   Gets the hash code for the inheritor instance.
  /// </summary>
  /// <returns>The hash code.</returns>
  protected abstract int InheritorGetHashCode();

  /// <summary>
  ///   Determines whether two <see cref="SymbolicExpression" /> instances are equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are equal; otherwise, <see langword="false" />.</returns>
  public static bool operator ==(SymbolicExpression? left, SymbolicExpression? right)
    => Equals(left, right);

  /// <summary>
  ///   Determines whether two <see cref="SymbolicExpression" /> instances are not equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are not equal; otherwise, <see langword="false" />.</returns>
  public static bool operator !=(SymbolicExpression? left, SymbolicExpression? right)
    => !Equals(left, right);
}
