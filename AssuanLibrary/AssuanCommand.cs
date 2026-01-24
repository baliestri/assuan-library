// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections;
using System.Collections.Immutable;

namespace AssuanLibrary;

/// <summary>
///   Represents an Assuan command with its name and arguments.
/// </summary>
public sealed class AssuanCommand : IEnumerable<string>, IEquatable<AssuanCommand> {
  private string[] _entries;

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanCommand" /> class.
  /// </summary>
  /// <param name="commandName">The name of the command.</param>
  /// <exception cref="ArgumentException">Thrown when <paramref name="commandName" /> is <see langword="null" /> or whitespace.</exception>
  public AssuanCommand(string commandName) {
    ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

    _entries = new string[INITIAL_COLLECTION_SIZE];
    _entries[0] = commandName.Trim();

    Count = 1;
  }

  /// <summary>
  ///   Gets or sets the entry at the specified index.
  /// </summary>
  /// <param name="index">The index of the entry.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index" /> is out of range.</exception>
  /// <remarks>The first entry (index 0) is the command name and cannot be removed.</remarks>
  public string this[int index] {
    get {
      if (index < 0 ||
          index >= Count) {
        throw new ArgumentOutOfRangeException(nameof(index), "The index is out of range.");
      }

      return _entries[index];
    }
    set {
      if (index < 0 ||
          index >= Count) {
        throw new ArgumentOutOfRangeException(nameof(index), "The index is out of range.");
      }

      if (string.IsNullOrWhiteSpace(value)) {
        RemoveAt(index);
        return;
      }

      _entries[index] = value.Trim();
    }
  }

  /// <summary>
  ///   The name of the command.
  /// </summary>
  public string Name => _entries[0];

  /// <summary>
  ///   The arguments of the command, if applicable.
  /// </summary>
  public ImmutableArray<string> Arguments => [.._entries.AsSpan(1)];

  /// <summary>
  ///   The current count of entries in the command.
  /// </summary>
  /// <remarks>The count includes the command name and all arguments.</remarks>
  public int Count { get; private set; }

  /// <inheritdoc />
  public IEnumerator<string> GetEnumerator() {
    for (var i = 0; i < Count; i++) {
      yield return _entries[i];
    }
  }

  /// <inheritdoc />
  IEnumerator IEnumerable.GetEnumerator()
    => GetEnumerator();

  /// <inheritdoc />
  public bool Equals(AssuanCommand? other) {
    if (other is null ||
        Count != other.Count) {
      return false;
    }

    return ReferenceEquals(this, other) ||
           _entries.SequenceEqual(other._entries);
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is AssuanCommand command && Equals(command);

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEntries()
      .Select(value => value.GetHashCode())
      .Aggregate(17, (current, hash) => (current * 31) + hash);

  /// <summary>
  ///   Adds an argument to the command.
  /// </summary>
  /// <param name="argument">The argument to add.</param>
  /// <exception cref="ArgumentException">Thrown when <paramref name="argument" /> is <see langword="null" /> or whitespace.</exception>
  public void Add(string argument) {
    ArgumentException.ThrowIfNullOrWhiteSpace(argument);

    if (Count == _entries.Length) {
      Array.Resize(ref _entries, _entries.Length + INITIAL_COLLECTION_SIZE);
    }

    var trimmedArgument = argument.Trim();

    _entries[Count++] = trimmedArgument.Contains(' ', StringComparison.Ordinal)
      ? $"¨{trimmedArgument}¨"
      : trimmedArgument;
  }

  /// <summary>
  ///   Removes an argument from the command.
  /// </summary>
  /// <param name="argument">The argument to remove.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="argument" /> was not found in the command.</exception>
  public void Remove(string argument) {
    ArgumentException.ThrowIfNullOrWhiteSpace(argument);

    var index = Array.IndexOf(_entries, argument, 0, Count);
    RemoveAt(index);
  }

  /// <summary>
  ///   Removes the argument at the specified index.
  /// </summary>
  /// <param name="index">The index of the argument to remove.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index" /> is out of range.</exception>
  /// <remarks>The first entry (index 0) is the command name and cannot be removed.</remarks>
  public void RemoveAt(int index) {
    if (index < 1 ||
        index >= Count) {
      throw new ArgumentOutOfRangeException(nameof(index), "The index is out of range.");
    }

    for (var i = index; i < (Count - 1); i++) {
      _entries[i] = _entries[i + 1];
    }

    _entries[--Count] = null!;
  }

  /// <inheritdoc />
  public override string ToString()
    => AssuanEncoder.AsString(string.Join(' ', this));

  /// <summary>
  ///   Returns a byte array representation of the command.
  /// </summary>
  /// <returns>A byte array representing the command.</returns>
  public byte[] ToBytes()
    => AssuanEncoder.AsBytes(string.Join(' ', this));

  /// <summary>
  ///   Returns a read-only memory representation of the command.
  /// </summary>
  /// <returns>A read-only memory representing the command.</returns>
  public ReadOnlyMemory<byte> ToReadOnlyMemory()
    => AssuanEncoder.AsReadOnlyMemory(string.Join(' ', this));

  /// <summary>
  ///   Determines whether two <see cref="AssuanCommand" /> instances are equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are equal; otherwise, <see langword="false" />.</returns>
  public static bool operator ==(AssuanCommand? left, AssuanCommand? right)
    => Equals(left, right);

  /// <summary>
  ///   Determines whether two <see cref="AssuanCommand" /> instances are not equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are not equal; otherwise, <see langword="false" />.</returns>
  public static bool operator !=(AssuanCommand? left, AssuanCommand? right)
    => !Equals(left, right);

  private IEnumerable<string> GetEntries() {
    for (var i = 0; i < Count; i++) {
      yield return _entries[i];
    }
  }
}
