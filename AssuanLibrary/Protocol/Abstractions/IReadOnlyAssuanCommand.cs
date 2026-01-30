// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Immutable;

namespace AssuanLibrary.Protocol.Abstractions;

/// <summary>
///   Represents a read-only Assuan command.
/// </summary>
public interface IReadOnlyAssuanCommand : IEnumerable<string>, IEquatable<IReadOnlyAssuanCommand> {
  /// <summary>
  ///   Gets the entry at the specified index.
  /// </summary>
  /// <param name="index">The index of the entry.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index" /> is out of range.</exception>
  string this[int index] { get; }

  /// <summary>
  ///   The name of the command.
  /// </summary>
  string Name { get; }

  /// <summary>
  ///   The arguments of the command, if applicable.
  /// </summary>
  ImmutableArray<string> Arguments { get; }

  /// <summary>
  ///   The current count of entries in the command.
  /// </summary>
  /// <remarks>The count includes the command name and all arguments.</remarks>
  int Count { get; }

  /// <summary>
  ///   Returns a byte array representation of the command.
  /// </summary>
  /// <returns>A byte array representing the command.</returns>
  byte[] ToBytes();

  /// <summary>
  ///   Returns a read-only memory representation of the command.
  /// </summary>
  /// <returns>A read-only memory representing the command.</returns>
  ReadOnlyMemory<byte> ToReadOnlyMemory();
}
