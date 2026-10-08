// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Diagnostics;

/// <summary>Optional synchronous observer; its failure cannot alter protocol processing.</summary>
public interface IAssuanEventSink {
  /// <summary>Observes only the fixed safe fields of a completed operation.</summary>
  void Emit(in AssuanEvent eventInfo);
}

internal static class AssuanEventDispatch {
  internal static void Report(IAssuanEventSink? sink, AssuanComponent component, AssuanEventId id,
  AssuanOutcome outcome, int byteCount = 0) {
    if (sink is null) {
      return;
    }

    var info = new AssuanEvent(component, id, outcome, byteCount);
    try { sink.Emit(in info); }
    catch (Exception) {
      /* Observers cannot change a protocol operation's result. */
    }
  }
}
