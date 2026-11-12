// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Diagnostics;

/// <summary>Optional synchronous observer; its failure cannot alter protocol processing.</summary>
public interface IAssuanEventSink {
  /// <summary>Observes only the fixed safe fields of a completed operation.</summary>
  void Emit(in AssuanEvent eventInfo);
}
