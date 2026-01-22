// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;

namespace AssuanLibrary.Network;

/// <summary>
///   Represents configuration options for an Assuan client.
/// </summary>
/// <param name="ipAddress">The IP address to connect to.</param>
/// <param name="portAndNonce">The port and nonce information to use when connecting.</param>
/// <param name="timeout">The timeout duration for receiving data.</param>
public sealed class AssuanClientOptions(IPAddress ipAddress, PortAndNonce portAndNonce, TimeSpan timeout) : IEquatable<AssuanClientOptions> {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanClientOptions" /> with default values.
  /// </summary>
  public static readonly AssuanClientOptions Empty = new();

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClientOptions" /> class with the specified port and nonce
  ///   and timeout, using the loopback IP address.
  /// </summary>
  /// <param name="portAndNonce">The port and nonce information to use when connecting.</param>
  /// <param name="timeout">The timeout duration for receiving data.</param>
  public AssuanClientOptions(PortAndNonce portAndNonce, TimeSpan timeout) : this(IPAddress.Loopback, portAndNonce, timeout) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClientOptions" /> class with the specified port and nonce,
  ///   using the loopback IP address.
  /// </summary>
  /// <param name="portAndNonce">The port and nonce information to use when connecting.</param>
  public AssuanClientOptions(PortAndNonce portAndNonce) : this(IPAddress.Loopback, portAndNonce, TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS)) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClientOptions" /> class using the loopback IP address
  ///   and an empty port and nonce.
  /// </summary>
  public AssuanClientOptions() : this(IPAddress.Loopback, PortAndNonce.Empty, TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS)) { }

  /// <summary>
  ///   The IP address to connect to.
  /// </summary>
  public IPAddress IpAddress {
    get;
    set {
      if (Equals(value, IPAddress.None) ||
          Equals(value, IPAddress.IPv6None)) {
        throw new NotSupportedException("The specified IP address is not supported.");
      }

      field = value;
    }
  } = ipAddress;

  /// <summary>
  ///   The port and nonce information to use when connecting.
  /// </summary>
  public PortAndNonce PortAndNonce {
    get;
    set {
      if (value.Port <= 0) {
        throw new ArgumentOutOfRangeException(nameof(PortAndNonce.Port), "The port must be a positive ushort.");
      }

      if (value.Nonce.Length == 0) {
        throw new ArgumentException("The nonce cannot be empty.", nameof(PortAndNonce.Nonce));
      }

      field = value;
    }
  } = portAndNonce;

  /// <summary>
  ///   The timeout duration for receiving data.
  /// </summary>
  public TimeSpan Timeout { get; set; } = timeout;

  /// <summary>
  ///   Indicates whether to throw an exception if the client is not connected when attempting to send or receive data.
  /// </summary>
  public bool ThrowIfNotConnected { get; set; } = true;

  /// <inheritdoc />
  public bool Equals(AssuanClientOptions? other) {
    if (other is null) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    return IpAddress.Equals(other.IpAddress) &&
           PortAndNonce.Equals(other.PortAndNonce) &&
           ThrowIfNotConnected == other.ThrowIfNotConnected;
  }

  /// <summary>
  ///   Deconstructs the <see cref="AssuanClientOptions" /> into its components.
  /// </summary>
  /// <param name="ipAddress">The IP address.</param>
  /// <param name="portAndNonce">The port and nonce information.</param>
  /// <param name="timeout">The timeout duration.</param>
  public void Deconstruct(out IPAddress ipAddress, out PortAndNonce portAndNonce, out TimeSpan timeout) {
    ipAddress = IpAddress;
    portAndNonce = PortAndNonce;
    timeout = Timeout;
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => ReferenceEquals(this, obj) || (obj is AssuanClientOptions other && Equals(other));

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEqualityComponents()
      .Select(obj => obj.GetHashCode())
      .Aggregate((x, y) => x ^ y);

  /// <summary>
  ///   Determines whether two <see cref="AssuanClientOptions" /> instances are equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are equal; otherwise, <see langword="false" />.</returns>
  public static bool operator ==(AssuanClientOptions? left, AssuanClientOptions? right)
    => Equals(left, right);

  /// <summary>
  ///   Determines whether two <see cref="AssuanClientOptions" /> instances are not equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are not equal; otherwise, <see langword="false" />.</returns>
  public static bool operator !=(AssuanClientOptions? left, AssuanClientOptions? right)
    => !Equals(left, right);

  private IEnumerable<object> GetEqualityComponents() {
    yield return IpAddress;
    yield return PortAndNonce;
    yield return Timeout;
    yield return ThrowIfNotConnected;
  }
}
