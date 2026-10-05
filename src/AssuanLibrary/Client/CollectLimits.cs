// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client;

/// <summary>Positive budgets for explicitly retained response bytes and observed events.</summary>
public sealed class CollectLimits {
  /// <summary>Rejects zero or negative budgets before starting a command.</summary>
  public CollectLimits(int maxBytes = 16 * 1024 * 1024, int maxEvents = 4096) {
    if (maxBytes <= 0 ||
        maxEvents <= 0) {
      throw new ClientException(ClientErrorKind.InvalidOptions);
    }

    MaxBytes = maxBytes;
    MaxEvents = maxEvents;
  }

  /// <summary>16 MiB of retained bytes and 4,096 observed events.</summary>
  public static CollectLimits Default { get; } = new();

  /// <summary>Maximum aggregate data, status and comment bytes.</summary>
  public int MaxBytes { get; }

  /// <summary>Maximum number of events including END and the final response.</summary>
  public int MaxEvents { get; }
}
