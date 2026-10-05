// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace AssuanLibrary.Transport;

/// <summary>One monotonic budget shared by every awaited operation in a transport call.</summary>
internal readonly struct TransportDeadline {
  private static readonly TimeSpan _longestSupportedTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);
  private readonly long _started;

  internal TransportDeadline(TimeSpan budget) {
    if (budget < TimeSpan.Zero ||
        budget > _longestSupportedTimeout) {
      throw new TransportException(TransportErrorKind.InvalidOptions);
    }

    if (budget == TimeSpan.Zero) {
      throw new TransportException(TransportErrorKind.Timeout);
    }

    Remaining = budget;
    _started = Stopwatch.GetTimestamp();
  }

  internal TimeSpan Remaining {
    get {
      var time = field - Stopwatch.GetElapsedTime(_started);
      return time > TimeSpan.Zero ? time : TimeSpan.Zero;
    }
  }
}
