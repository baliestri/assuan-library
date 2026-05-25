// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Logging;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>
///   Represents configuration options for an Assuan client.
/// </summary>
public sealed class AssuanClientOptions : IEquatable<AssuanClientOptions> {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanClientOptions" /> with default values.
  /// </summary>
  public static readonly AssuanClientOptions Default = CreateDefault();

  /// <summary>
  ///   Indicates whether to throw an exception if the client is not connected when attempting to send or receive data.
  /// </summary>
  public bool ThrowIfNotConnected { get; set; }

  /// <summary>
  ///   Options for configuring the Assuan connection.
  /// </summary>
  public Action<AssuanConnectionOptions>? ConfigureConnection { get; set; }

  /// <summary>
  ///   A callback that is invoked when a session is authenticating, providing authentication context.
  /// </summary>
  public AsyncClientHook<IReadOnlyDictionary<string, object>>? OnSessionAuthenticatingAsync { get; set; }

  /// <summary>
  ///   A callback that is invoked when a session is started.
  /// </summary>
  public AsyncClientHook<IReadOnlyDictionary<string, object>>? OnSessionStartedAsync { get; set; }

  /// <summary>
  ///   A callback that is invoked when a session is ending.
  /// </summary>
  public AsyncClientHook? OnSessionEndingAsync { get; set; }

  /// <summary>
  ///   Gets or sets logging options for the Assuan client.
  /// </summary>
  public AssuanLoggingOptions Logging { get; set; } = AssuanLoggingOptions.CreateDefault();

  /// <inheritdoc />
  public bool Equals(AssuanClientOptions? other) {
    if (other is null) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    return ThrowIfNotConnected == other.ThrowIfNotConnected &&
           ConfigureConnection == other.ConfigureConnection &&
           OnSessionAuthenticatingAsync == other.OnSessionAuthenticatingAsync &&
           OnSessionStartedAsync == other.OnSessionStartedAsync &&
           OnSessionEndingAsync == other.OnSessionEndingAsync &&
           Equals(Logging, other.Logging);
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => ReferenceEquals(this, obj) || (obj is AssuanClientOptions other && Equals(other));

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEqualityComponents()
      .Select(obj => obj?.GetHashCode())
      .Aggregate(17, (current, hash) => hash.HasValue ? (current * 31) + hash.Value : current);

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

  private IEnumerable<object?> GetEqualityComponents() {
    yield return ThrowIfNotConnected;
    yield return ConfigureConnection;
    yield return OnSessionAuthenticatingAsync;
    yield return OnSessionStartedAsync;
    yield return OnSessionEndingAsync;
    yield return Logging;
  }

  internal static AssuanClientOptions CreateDefault()
    => new() {
      ThrowIfNotConnected = true,
      ConfigureConnection = null,
      OnSessionAuthenticatingAsync = null,
      OnSessionStartedAsync = null,
      OnSessionEndingAsync = null,
      Logging = AssuanLoggingOptions.CreateDefault()
    };
}
