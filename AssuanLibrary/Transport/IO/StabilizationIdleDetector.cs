// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Transport.IO;

[ExcludeFromCodeCoverage]
internal static class StabilizationIdleDetector {
  /// <summary>
  ///   Updates the idle detector state based on whether data was received.
  /// </summary>
  /// <param name="hadData">Indicates whether data was received during the last check.</param>
  /// <param name="elapsed">The elapsed time since the last check.</param>
  /// <param name="idleElapsed">The elapsed idle time.</param>
  /// <param name="idleCycles">The number of idle cycles.</param>
  public static void Update(bool hadData, TimeSpan elapsed, ref TimeSpan idleElapsed, ref int idleCycles) {
    if (hadData) {
      idleElapsed = TimeSpan.Zero;
      idleCycles = 0;
      return;
    }

    idleElapsed += elapsed;
    idleCycles++;
  }

  /// <summary>
  ///   Determines whether the idle time has reached the required delay.
  /// </summary>
  /// <param name="idleElapsed">The elapsed idle time.</param>
  /// <param name="requiredDelay">The required delay to consider the state as stable.</param>
  /// <returns><see langword="true" /> if the idle time has reached the required delay; otherwise, <see langword="false" />.</returns>
  public static bool IsStable(TimeSpan idleElapsed, TimeSpan requiredDelay)
    => idleElapsed >= requiredDelay;
}
