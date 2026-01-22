// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.Versioning;

namespace AssuanLibrary.Network.Platform.Windows;

/// <summary>
///   Represents a network port along with its associated nonce.
/// </summary>
/// <param name="Port">The network port number.</param>
/// <param name="Nonce">The associated nonce as a read-only memory of bytes.</param>
[SupportedOSPlatform("windows")]
public readonly record struct PortAndNonce(ushort Port, ReadOnlyMemory<byte> Nonce) {
  /// <summary>
  ///   A read-only instance of <see cref="PortAndNonce" /> with default values.
  /// </summary>
  public static readonly PortAndNonce Empty = new(0, ReadOnlyMemory<byte>.Empty);

  /// <summary>
  ///   Initializes a new instance of the <see cref="PortAndNonce" /> record struct
  ///   using a byte array for the nonce.
  /// </summary>
  /// <param name="port">The network port number.</param>
  /// <param name="nonce">The associated nonce as a byte array.</param>
  public PortAndNonce(ushort port, byte[] nonce) : this(port, new ReadOnlyMemory<byte>(nonce)) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="PortAndNonce" /> record struct
  ///   using a read-only span for the nonce.
  /// </summary>
  /// <param name="port">The network port number.</param>
  /// <param name="nonce">The associated nonce as a read-only span of bytes.</param>
  public PortAndNonce(ushort port, ReadOnlySpan<byte> nonce) : this(port, nonce.ToArray()) { }
}
