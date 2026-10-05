// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport;

/// <summary>Verified Unix credentials captured by the operating system during accept.</summary>
public sealed class UnixPeerIdentity : PeerIdentity {
  internal UnixPeerIdentity(uint uid, uint gid, uint? pid) {
    Uid = uid;
    Gid = gid;
    Pid = pid;
  }

  /// <summary>The peer's effective user ID.</summary>
  public uint Uid { get; }

  /// <summary>The peer's effective group ID.</summary>
  public uint Gid { get; }

  /// <summary>The peer's process ID when the platform supplies one.</summary>
  public uint? Pid { get; }

  /// <inheritdoc />
  public override string ToString()
    => "UnixPeerIdentity { .. }";
}
