// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   The exception that is thrown when a symbolic expression is incomplete.
/// </summary>
/// <param name="message">The exception message.</param>
public sealed class InvalidBinaryLengthException(string message) : SymbolicExpressionException(message);
