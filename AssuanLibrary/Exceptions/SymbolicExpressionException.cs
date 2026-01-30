// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   The exception that is thrown when an error occurs while processing a symbolic expression.
/// </summary>
public abstract class SymbolicExpressionException : FormatException {
  /// <inheritdoc />
  protected SymbolicExpressionException(string message) : base(message) { }

  /// <inheritdoc />
  protected SymbolicExpressionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
///   The exception that is thrown when a symbolic expression is incomplete.
/// </summary>
/// <param name="message">The exception message.</param>
public sealed class UnexpectedEndOfInputException(string message) : SymbolicExpressionException(message);
