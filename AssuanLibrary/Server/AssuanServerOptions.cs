// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>
///   Represents configuration options for an Assuan server.
/// </summary>
public sealed class AssuanServerOptions : IEquatable<AssuanServerOptions> {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanServerOptions" /> with default values.
  /// </summary>
  public static readonly AssuanServerOptions Default = new() {
    Banner = "Assuan Server Ready"
  };

  /// <summary>
  ///   The banner message to send upon connection establishment.
  /// </summary>
  public string? Banner { get; set; }

  /// <summary>
  ///   Indicates whether to send the banner message upon connection establishment.
  /// </summary>
  [MemberNotNullWhen(true, nameof(Banner))]
  public bool SendBanner => !string.IsNullOrWhiteSpace(Banner);

  /// <summary>
  ///   Options for configuring the Assuan listener.
  /// </summary>
  public Action<AssuanListenerOptions>? ConfigureListener { get; set; }

  /// <summary>
  ///   A callback that is invoked when a session is authenticating.
  /// </summary>
  public AsyncServerHook? OnAuthenticateSessionAsync { get; set; }

  /// <inheritdoc />
  public bool Equals(AssuanServerOptions? other) {
    if (other is null) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    return Banner == other.Banner &&
           ConfigureListener == other.ConfigureListener &&
           OnAuthenticateSessionAsync == other.OnAuthenticateSessionAsync;
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => ReferenceEquals(this, obj) || (obj is AssuanServerOptions other && Equals(other));

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEqualityComponents()
      .Select(obj => obj?.GetHashCode())
      .Aggregate(17, (current, hash) => hash.HasValue ? (current * 31) + hash.Value : current);

  /// <summary>
  ///   Determines whether two <see cref="AssuanServerOptions" /> instances are equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are equal; otherwise, <see langword="false" />.</returns>
  public static bool operator ==(AssuanServerOptions? left, AssuanServerOptions? right)
    => Equals(left, right);

  /// <summary>
  ///   Determines whether two <see cref="AssuanServerOptions" /> instances are not equal.
  /// </summary>
  /// <param name="left">The left instance.</param>
  /// <param name="right">The right instance.</param>
  /// <returns><see langword="true" /> if the two instances are not equal; otherwise, <see langword="false" />.</returns>
  public static bool operator !=(AssuanServerOptions? left, AssuanServerOptions? right)
    => !Equals(left, right);

  private IEnumerable<object?> GetEqualityComponents() {
    yield return Banner;
    yield return ConfigureListener;
    yield return OnAuthenticateSessionAsync;
  }
}
