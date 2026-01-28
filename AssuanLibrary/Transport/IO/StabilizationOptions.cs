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
    TimeSpan.FromMilliseconds(100),
    TimeSpan.FromMilliseconds(50)
  );

  private StabilizationOptions(TimeSpan delay, TimeSpan pollInterval)
    => (Delay, PollInterval) = (delay, pollInterval);

  /// <summary>
  ///   The delay duration to consider the stream stabilized after no data has been received.
  /// </summary>
  public TimeSpan Delay { get; set; }

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
  /// <param name="pollInterval">The poll interval.</param>
  public void Deconstruct(out TimeSpan delay, out TimeSpan pollInterval)
    => (delay, pollInterval) = (Delay, PollInterval);

  private IEnumerable<object> GetEqualityComponents() {
    yield return Delay;
    yield return PollInterval;
  }
}
