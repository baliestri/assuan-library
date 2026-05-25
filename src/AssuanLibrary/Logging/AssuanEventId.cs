// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Logging;

/// <summary>
///   Identifies an Assuan log event.
/// </summary>
public readonly struct AssuanEventId : IEquatable<AssuanEventId> {
  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanEventId" /> struct.
  /// </summary>
  /// <param name="id">The numeric identifier.</param>
  /// <param name="name">The event name.</param>
  public AssuanEventId(int id, string? name = null) {
    Id = id;
    Name = name;
  }

  /// <summary>
  ///   Gets the numeric identifier.
  /// </summary>
  public int Id { get; }

  /// <summary>
  ///   Gets the event name.
  /// </summary>
  public string? Name { get; }

  /// <inheritdoc />
  public bool Equals(AssuanEventId other)
    => Id == other.Id && Name == other.Name;

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is AssuanEventId other && Equals(other);

  /// <inheritdoc />
  public override int GetHashCode()
    => ((Id.GetHashCode() * 397) ^ (Name?.GetHashCode() ?? 0));

  /// <inheritdoc />
  public override string ToString()
    => Name is null ? Id.ToString() : $"{Name}({Id})";

  /// <summary>
  ///   Determines whether two <see cref="AssuanEventId" /> instances are equal.
  /// </summary>
  public static bool operator ==(AssuanEventId left, AssuanEventId right)
    => left.Equals(right);

  /// <summary>
  ///   Determines whether two <see cref="AssuanEventId" /> instances are not equal.
  /// </summary>
  public static bool operator !=(AssuanEventId left, AssuanEventId right)
    => !left.Equals(right);
}
