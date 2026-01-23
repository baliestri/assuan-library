// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Expressions;

/// <summary>
///   Represents an atomic symbolic expression.
/// </summary>
/// <param name="value">The value of the atom.</param>
public sealed class SymbolicExpressionAtom(ReadOnlySpan<byte> value) : SymbolicExpression {
  /// <inheritdoc />
  public override SymbolicExpressionType Type => SymbolicExpressionType.Atom;

  /// <summary>
  ///   The value of the atom.
  /// </summary>
  public byte[] Value { get; } = value.ToArray();

  /// <inheritdoc />
  public override string ToString()
    => $"Atom[{AssuanDecoder.ToString(Value)}]";
}
