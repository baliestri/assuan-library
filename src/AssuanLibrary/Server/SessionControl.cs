// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>Internal host control that closes a session without releasing an active handler's state.</summary>
internal sealed class SessionControl(Action requestStop) {
  internal void RequestStop()
    => requestStop();
}
