// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Kind of an existing agent's resolved local endpoint.</summary>
public enum AgentEndpointKind {
  /// <summary>An existing Unix-domain socket addressed by original path bytes.</summary>
  UnixSocket,

  /// <summary>A native Windows loopback socket requiring its private nonce.</summary>
  NativeWindowsAgent
}
