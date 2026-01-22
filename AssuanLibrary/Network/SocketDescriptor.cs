// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Network;

/// <summary>
///   Represents a smart enum for which socket file to use.
/// </summary>
public sealed class SocketDescriptor : IEquatable<SocketDescriptor> {
  /// <summary>
  ///   Uses <c>agent-socket</c> as the socket descriptor.
  /// </summary>
  public static readonly SocketDescriptor AgentSocket = new("agent-socket");

  /// <summary>
  ///   Uses <c>dirmngr-socket</c> as the socket descriptor.
  /// </summary>
  public static readonly SocketDescriptor DirmngrSocket = new("dirmngr-socket");

  /// <summary>
  ///   Uses <c>keyboxd-socket</c> as the socket descriptor.
  /// </summary>
  public static readonly SocketDescriptor KeyboxdSocket = new("keyboxd-socket");

  private readonly string _descriptor;

  private SocketDescriptor(string descriptor)
    => _descriptor = descriptor;

  /// <inheritdoc />
  public bool Equals(SocketDescriptor? other) {
    if (other is null) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    return _descriptor == other._descriptor;
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => ReferenceEquals(this, obj) || (obj is SocketDescriptor other && Equals(other));

  /// <inheritdoc />
  public override int GetHashCode()
    => _descriptor.GetHashCode();

  /// <inheritdoc />
  public override string ToString()
    => _descriptor;

  /// <summary>
  ///   Determines whether two <see cref="SocketDescriptor" /> instances are equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are equal; otherwise, <see langword="false" />.</returns>
  public static bool operator ==(SocketDescriptor? left, SocketDescriptor? right)
    => Equals(left, right);

  /// <summary>
  ///   Determines whether two <see cref="SocketDescriptor" /> instances are not equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are not equal; otherwise, <see langword="false" />.</returns>
  public static bool operator !=(SocketDescriptor? left, SocketDescriptor? right)
    => !Equals(left, right);

  /// <summary>
  ///   Implicitly converts a <see cref="SocketDescriptor" /> to its underlying string representation.
  /// </summary>
  /// <param name="descriptor">The <see cref="SocketDescriptor" /> instance to convert.</param>
  /// <returns>The string representation of the socket descriptor.</returns>
  public static implicit operator string(SocketDescriptor descriptor)
    => descriptor._descriptor;
}
