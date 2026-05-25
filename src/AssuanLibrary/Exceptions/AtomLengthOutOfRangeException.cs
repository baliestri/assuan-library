// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   The exception that is thrown when the declared length of an atom exceeds its actual length.
/// </summary>
/// <param name="declaredLength">The declared length of the atom.</param>
/// <param name="actualLength">The actual length of the atom.</param>
/// <param name="position">The position in the input where the error occurred.</param>
public sealed class AtomLengthOutOfRangeException(int declaredLength, int actualLength, int position) : SymbolicExpressionException(
  $"The declared length of the atom ({declaredLength}) exceeds actual length ({actualLength}) at position {position}.") {
  /// <summary>
  ///   The declared length of the atom.
  /// </summary>
  public int DeclaredLength { get; } = declaredLength;

  /// <summary>
  ///   The actual length of the atom.
  /// </summary>
  public int ActualLength { get; } = actualLength;

  /// <summary>
  ///   The position in the input where the error occurred.
  /// </summary>
  public int Position { get; } = position;
}
