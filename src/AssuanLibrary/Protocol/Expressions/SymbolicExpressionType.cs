// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol.Expressions;

/// <summary>
///   Defines the types of symbolic expressions.
/// </summary>
public enum SymbolicExpressionType {
  /// <summary>
  ///   An atomic symbolic expression (a single token with a value).
  /// </summary>
  Atom,

  /// <summary>
  ///   A collection of symbolic expressions.
  /// </summary>
  Collection
}
