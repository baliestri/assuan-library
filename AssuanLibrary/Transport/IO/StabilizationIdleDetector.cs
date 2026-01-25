// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Transport.IO;

[ExcludeFromCodeCoverage]
internal static class StabilizationIdleDetector {
  public static void UpdateZeroState(int available, ref int consecutiveZeros, ref DateTime zeroStartedAt) {
    if (available == 0) {
      consecutiveZeros++;
      if (consecutiveZeros == 1) {
        zeroStartedAt = DateTime.UtcNow;
      }

      return;
    }

    consecutiveZeros = 0;
    zeroStartedAt = default;
  }

  public static bool IsStableIdle(int consecutiveZeros, DateTime zeroStartedAt, TimeSpan stabilizationDelay) {
    if (consecutiveZeros == 0) {
      return false;
    }

    var elapsed = DateTime.UtcNow - zeroStartedAt;
    return elapsed >= stabilizationDelay;
  }
}
