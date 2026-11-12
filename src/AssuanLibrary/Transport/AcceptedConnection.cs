// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport;

/// <summary>An accepted duplex byte stream; the receiver owns and must dispose its stream.</summary>
public sealed class AcceptedConnection {
  /// <summary>Wraps a custom or TCP stream without claiming OS-authenticated identity.</summary>
  public AcceptedConnection(Stream stream) : this(stream, null) { }

  internal AcceptedConnection(Stream stream, PeerIdentity? peerIdentity) {
    Stream = stream ?? throw new ArgumentNullException(nameof(stream));
    PeerIdentity = peerIdentity;
  }

  /// <summary>The connected stream whose ownership passes to the receiver.</summary>
  public Stream Stream { get; }

  /// <summary>Verified operating-system identity, or null for TCP and unverified custom transports.</summary>
  public PeerIdentity? PeerIdentity { get; }

  /// <inheritdoc />
  public override string ToString()
    => "AcceptedConnection { .. }";
}
