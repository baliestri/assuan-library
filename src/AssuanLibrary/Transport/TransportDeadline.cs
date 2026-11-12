// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace AssuanLibrary.Transport;

/// <summary>One monotonic budget shared by every awaited operation in a transport call.</summary>
internal readonly struct TransportDeadline {
  private readonly static TimeSpan LongestSupportedTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);
  private readonly long _started;
  private readonly TimeProvider? _provider;

  internal TransportDeadline(TimeSpan budget, TimeProvider? provider = null) {
    if (budget < TimeSpan.Zero ||
        budget > LongestSupportedTimeout) {
      throw new TransportException(TransportErrorKind.InvalidOptions);
    }

    if (budget == TimeSpan.Zero) {
      throw new TransportException(TransportErrorKind.Timeout);
    }

    Remaining = budget;
    _provider = provider;
    _started = provider?.GetTimestamp() ?? Stopwatch.GetTimestamp();
  }

  internal TimeSpan Remaining {
    get
    {
      var time = field - (_provider?.GetElapsedTime(_started) ?? Stopwatch.GetElapsedTime(_started));
      return time > TimeSpan.Zero ? time : TimeSpan.Zero;
    }
  }
}
