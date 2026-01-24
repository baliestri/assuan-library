// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Network;

/// <summary>
///   Represents configuration options for an Assuan client.
/// </summary>
/// <param name="socketDescriptor">The socket descriptor to use when connecting.</param>
/// <param name="timeout">The timeout duration for receiving data.</param>
public sealed class AssuanClientOptions(SocketDescriptor socketDescriptor, TimeSpan timeout) : IEquatable<AssuanClientOptions> {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanClientOptions" /> with default values.
  /// </summary>
  public static readonly AssuanClientOptions Empty = new();

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClientOptions" /> class with the specified port and nonce,
  ///   using the loopback IP address.
  /// </summary>
  /// <param name="socketDescriptor">The socket descriptor to use when connecting.</param>
  public AssuanClientOptions(SocketDescriptor socketDescriptor) : this(socketDescriptor, TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS)) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClientOptions" /> class using the loopback IP address
  ///   and an empty port and nonce.
  /// </summary>
  public AssuanClientOptions() : this(SocketDescriptor.AgentSocket, TimeSpan.FromSeconds(DEFAULT_TIMEOUT_SECONDS)) { }

  /// <summary>
  ///   The port and nonce information to use when connecting.
  /// </summary>
  public SocketDescriptor SocketDescriptor { get; set; } = socketDescriptor;

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

    return SocketDescriptor.Equals(other.SocketDescriptor) &&
           ThrowIfNotConnected == other.ThrowIfNotConnected;
  }

  /// <summary>
  ///   Deconstructs the <see cref="AssuanClientOptions" /> into its components.
  /// </summary>
  /// <param name="socketDescriptor">The socket descriptor.</param>
  /// <param name="timeout">The timeout duration.</param>
  public void Deconstruct(out SocketDescriptor socketDescriptor, out TimeSpan timeout) {
    socketDescriptor = SocketDescriptor;
    timeout = Timeout;
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => ReferenceEquals(this, obj) || (obj is AssuanClientOptions other && Equals(other));

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEqualityComponents()
      .Select(obj => obj.GetHashCode())
      .Aggregate(17, (current, hash) => (current * 31) + hash);

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
    yield return SocketDescriptor;
    yield return Timeout;
    yield return ThrowIfNotConnected;
  }
}
