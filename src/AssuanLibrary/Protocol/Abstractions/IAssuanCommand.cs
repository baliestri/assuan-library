// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol.Abstractions;

/// <summary>
///   Represents a mutable Assuan command.
/// </summary>
public interface IAssuanCommand : IReadOnlyAssuanCommand {
  /// <summary>
  ///   Gets or sets the entry at the specified index.
  /// </summary>
  /// <param name="index">The index of the entry.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index" /> is out of range.</exception>
  /// <remarks>The first entry (index 0) is the command name and cannot be removed.</remarks>
  new string this[int index] { get; set; }

  /// <summary>
  ///   Adds an argument to the command.
  /// </summary>
  /// <param name="argument">The argument to add.</param>
  /// <exception cref="ArgumentException">Thrown when <paramref name="argument" /> is <see langword="null" /> or whitespace.</exception>
  void Add(string argument);

  /// <summary>
  ///   Removes an argument from the command.
  /// </summary>
  /// <param name="argument">The argument to remove.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="argument" /> was not found in the command.</exception>
  void Remove(string argument);

  /// <summary>
  ///   Removes the argument at the specified index.
  /// </summary>
  /// <param name="index">The index of the argument to remove.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index" /> is out of range.</exception>
  /// <remarks>The first entry (index 0) is the command name and cannot be removed.</remarks>
  void RemoveAt(int index);
}
