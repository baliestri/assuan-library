// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport;

/// <summary>OS-authenticated metadata for a peer, available only when verified by a transport.</summary>
public abstract class PeerIdentity {
  private protected PeerIdentity() { }
}
