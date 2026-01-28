// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Transport.Endpoints;

/// <summary>
///   Represents the kind of Assuan endpoint to connect to.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Smart enum pattern.")]
public sealed class AssuanEndpointKind : IEquatable<AssuanEndpointKind> {
  /// <summary>
  ///   Uses <c>agent-socket</c> as the endpoint kind.
  /// </summary>
  public static readonly AssuanEndpointKind AGENT = new("agent-socket");

  /// <summary>
  ///   Uses <c>dirmngr-socket</c> as the endpoint kind.
  /// </summary>
  public static readonly AssuanEndpointKind DIRMNGR = new("dirmngr-socket");

  /// <summary>
  ///   Uses <c>keyboxd-socket</c> as the endpoint kind.
  /// </summary>
  public static readonly AssuanEndpointKind KEYBOXD = new("keyboxd-socket");

  private readonly string _descriptor;

  private AssuanEndpointKind(string descriptor)
    => _descriptor = descriptor;

  /// <inheritdoc />
  public bool Equals(AssuanEndpointKind? other) {
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
    => ReferenceEquals(this, obj) || (obj is AssuanEndpointKind other && Equals(other));

  /// <inheritdoc />
  public override int GetHashCode()
    => _descriptor.GetHashCode();

  /// <inheritdoc />
  public override string ToString()
    => _descriptor;

  /// <summary>
  ///   Determines whether two <see cref="AssuanEndpointKind" /> instances are equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are equal; otherwise, <see langword="false" />.</returns>
  public static bool operator ==(AssuanEndpointKind? left, AssuanEndpointKind? right)
    => Equals(left, right);

  /// <summary>
  ///   Determines whether two <see cref="AssuanEndpointKind" /> instances are not equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are not equal; otherwise, <see langword="false" />.</returns>
  public static bool operator !=(AssuanEndpointKind? left, AssuanEndpointKind? right)
    => !Equals(left, right);

  /// <summary>
  ///   Implicitly converts a <see cref="AssuanEndpointKind" /> to its underlying string representation.
  /// </summary>
  /// <param name="descriptor">The <see cref="AssuanEndpointKind" /> instance to convert.</param>
  /// <returns>The string representation of the socket descriptor.</returns>
  public static implicit operator string(AssuanEndpointKind descriptor)
    => descriptor._descriptor;
}
