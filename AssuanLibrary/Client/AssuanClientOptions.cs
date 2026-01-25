// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>
///   Represents configuration options for an Assuan client.
/// </summary>
public sealed class AssuanClientOptions : IEquatable<AssuanClientOptions> {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanClientOptions" /> with default values.
  /// </summary>
  public static readonly AssuanClientOptions Default = new() {
    EnablePinentryLoopback = true,
    ConnectionOptions = AssuanConnectionOptions.Default
  };

  /// <summary>
  ///   Indicates whether to use pinentry loopback mode.
  /// </summary>
  public bool EnablePinentryLoopback { get; set; }

  /// <summary>
  ///   Options for configuring the Assuan connection.
  /// </summary>
  public required AssuanConnectionOptions ConnectionOptions { get; set; }

  /// <inheritdoc />
  public bool Equals(AssuanClientOptions? other) {
    if (other is null) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    return EnablePinentryLoopback == other.EnablePinentryLoopback &&
           ConnectionOptions.Equals(other.ConnectionOptions);
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
    yield return EnablePinentryLoopback;
    yield return ConnectionOptions;
  }
}
