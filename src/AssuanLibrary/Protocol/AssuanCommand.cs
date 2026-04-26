// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections;
using System.Collections.Immutable;
using AssuanLibrary.Extensions;
using AssuanLibrary.Protocol.Abstractions;

namespace AssuanLibrary.Protocol;

/// <inheritdoc cref="IAssuanCommand" />
public sealed class AssuanCommand : IAssuanCommand, IEquatable<AssuanCommand> {
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
  ///   Initializes a new instance of the <see cref="AssuanCommand" /> class from a byte array.
  /// </summary>
  /// <param name="buffer">The byte array representing the command.</param>
  /// <exception cref="ArgumentException">Thrown when <paramref name="buffer" /> does not contain a valid command.</exception>
  public AssuanCommand(byte[] buffer) {
    var commandString = AssuanDecoder.ToString(buffer.Take(Characters.LINE_FEED));
    var parts = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length == 0) {
      throw new ArgumentException("The command buffer does not contain a valid command.", nameof(buffer));
    }

    _entries = new string[parts.Length];
    Array.Copy(parts, _entries, parts.Length);

    Count = parts.Length;
  }

  /// <inheritdoc cref="IAssuanCommand.this" />
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

  /// <inheritdoc />
  public string Name => _entries[0];

  /// <inheritdoc />
  public ImmutableArray<string> Arguments => [.._entries.AsSpan(1)];

  /// <inheritdoc />
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
  public bool Equals(IReadOnlyAssuanCommand? other) {
    if (other is null ||
        Count != other.Count) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    var otherCommand = other as AssuanCommand;

    return otherCommand is not null &&
           otherCommand._entries.SequenceEqual(_entries);
  }

  /// <inheritdoc />
  public void Add(string argument) {
    ArgumentException.ThrowIfNullOrWhiteSpace(argument);

    if (Count == _entries.Length) {
      Array.Resize(ref _entries, _entries.Length + INITIAL_COLLECTION_SIZE);
    }

    var trimmedArgument = argument.Trim();

    _entries[Count++] = trimmedArgument;
  }

  /// <inheritdoc />
  public void Remove(string argument) {
    ArgumentException.ThrowIfNullOrWhiteSpace(argument);

    var index = Array.IndexOf(_entries, argument, 0, Count);
    RemoveAt(index);
  }

  /// <inheritdoc />
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
  public byte[] ToBytes()
    => AssuanEncoder.AsBytes(string.Join(' ', this));

  /// <inheritdoc />
  public ReadOnlyMemory<byte> ToReadOnlyMemory()
    => AssuanEncoder.AsReadOnlyMemory(string.Join(' ', this));

  /// <inheritdoc />
  public bool Equals(AssuanCommand? other) {
    if (other is null ||
        Count != other.Count) {
      return false;
    }

    return ReferenceEquals(this, other) ||
           other._entries.SequenceEqual(_entries);
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is AssuanCommand command && Equals(command);

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEntries()
      .Select(value => value.GetHashCode())
      .Aggregate(17, (current, hash) => (current * 31) + hash);

  /// <inheritdoc />
  public override string ToString()
    => AssuanEncoder.AsString(string.Join(' ', this));

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
