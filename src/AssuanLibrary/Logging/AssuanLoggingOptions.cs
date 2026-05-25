// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Logging;

/// <summary>
///   Represents Assuan logging options.
/// </summary>
public sealed class AssuanLoggingOptions : IEquatable<AssuanLoggingOptions> {
  /// <summary>
  ///   Default maximum number of decoded payload characters written when raw payload logging is enabled.
  /// </summary>
  public const int DEFAULT_MAX_PAYLOAD_LENGTH = 512;

  /// <summary>
  ///   Gets or sets the logger used by Assuan components.
  /// </summary>
  public IAssuanLogger Logger { get; set; } = NullAssuanLogger.Instance;

  /// <summary>
  ///   Gets or sets how payloads are represented in logs.
  /// </summary>
  public AssuanPayloadLoggingMode PayloadMode { get; set; } = AssuanPayloadLoggingMode.Redacted;

  /// <summary>
  ///   Gets or sets the maximum decoded payload length when <see cref="PayloadMode" /> is <see cref="AssuanPayloadLoggingMode.Raw" />.
  /// </summary>
  public int MaxPayloadLength { get; set; } = DEFAULT_MAX_PAYLOAD_LENGTH;

  /// <inheritdoc />
  public bool Equals(AssuanLoggingOptions? other) {
    if (other is null) {
      return false;
    }

    return ReferenceEquals(this, other) ||
           (Equals(Logger, other.Logger) &&
            PayloadMode == other.PayloadMode &&
            MaxPayloadLength == other.MaxPayloadLength);
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is AssuanLoggingOptions other && Equals(other);

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEqualityComponents()
      .Select(obj => obj?.GetHashCode())
      .Aggregate(17, (current, hash) => hash.HasValue ? (current * 31) + hash.Value : current);

  internal static AssuanLoggingOptions CreateDefault()
    => new();

  private IEnumerable<object?> GetEqualityComponents() {
    yield return Logger;
    yield return PayloadMode;
    yield return MaxPayloadLength;
  }
}
