// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.IO;

/// <summary>
///   Options for stream stabilization behavior.
/// </summary>
public sealed class StabilizationOptions : IEquatable<StabilizationOptions> {
  /// <summary>
  ///   A read-only instance of <see cref="StabilizationOptions" /> with default settings.
  /// </summary>
  public static readonly StabilizationOptions Default = new(
    TimeSpan.FromMilliseconds(150),
    TimeSpan.FromMilliseconds(40),
    TimeSpan.FromMilliseconds(40)
  );

  private StabilizationOptions(TimeSpan delay, TimeSpan gracePeriod, TimeSpan pollInterval)
    => (Delay, GracePeriod, PollInterval) = (delay, gracePeriod, pollInterval);

  /// <summary>
  ///   The delay duration to consider the stream stabilized after no data has been received.
  /// </summary>
  public TimeSpan Delay { get; set; }

  /// <summary>
  ///   The grace period to wait after receiving data before checking for more data.
  /// </summary>
  public TimeSpan GracePeriod { get; set; }

  /// <summary>
  ///   The interval at which to poll for new data.
  /// </summary>
  public TimeSpan PollInterval { get; set; }

  /// <inheritdoc />
  public bool Equals(StabilizationOptions? other) {
    if (other is null) {
      return false;
    }

    if (ReferenceEquals(this, other)) {
      return true;
    }

    return Delay == other.Delay &&
           GracePeriod == other.GracePeriod &&
           PollInterval == other.PollInterval;
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => ReferenceEquals(this, obj) || (obj is StabilizationOptions other && Equals(other));

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEqualityComponents()
      .Select(obj => obj.GetHashCode())
      .Aggregate(17, (current, hash) => (current * 31) + hash);

  /// <summary>
  ///   Deconstructs the <see cref="StabilizationOptions" /> into its constituent properties.
  /// </summary>
  /// <param name="delay">The delay duration.</param>
  /// <param name="gracePeriod">The grace period.</param>
  /// <param name="pollInterval">The poll interval.</param>
  public void Deconstruct(out TimeSpan delay, out TimeSpan gracePeriod, out TimeSpan pollInterval)
    => (delay, gracePeriod, pollInterval) = (Delay, GracePeriod, PollInterval);

  private IEnumerable<object> GetEqualityComponents() {
    yield return Delay;
    yield return GracePeriod;
    yield return PollInterval;
  }
}
