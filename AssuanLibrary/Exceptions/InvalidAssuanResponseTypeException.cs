// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Exceptions;

/// <summary>
///   The exception that is thrown when an invalid Assuan response type is encountered while parsing a symbolic expression.
/// </summary>
/// <param name="actual">The actual Assuan response type encountered.</param>
public sealed class InvalidAssuanResponseTypeException(AssuanResponseType actual)
  : SymbolicExpressionException($"Assuan response type '{actual}' is invalid for parsing a symbolic expression.");
