// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   The exception that is thrown when a symbolic expression is incomplete.
/// </summary>
/// <param name="position">The position where the error occurred.</param>
/// <param name="message">The error message.</param>
public sealed class InvalidSymbolicExpressionSyntaxException(int position, string message)
  : SymbolicExpressionException(string.Format("{1} (at position {0}).", position, message)) {
  /// <summary>
  ///   The position where the error occurred.
  /// </summary>
  public int Position { get; } = position;
}
