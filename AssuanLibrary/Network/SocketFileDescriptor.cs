// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Network;

/// <summary>
///   Represents a socket file descriptor used for inter-process communication.
/// </summary>
public sealed class SocketFileDescriptor {
  /// <summary>
  ///   Socket file descriptor for the agent-extra-socket.
  /// </summary>
  public static readonly SocketFileDescriptor AgentExtraSocket = new("agent-extra-socket");

  /// <summary>
  ///   Socket file descriptor for the dirmngr-socket.
  /// </summary>
  public static readonly SocketFileDescriptor DirmngrSocket = new("dirmngr-socket");

  /// <summary>
  ///   Socket file descriptor for the keyboxd-socket.
  /// </summary>
  public static readonly SocketFileDescriptor KeyboxdSocket = new("keyboxd-socket");

  private readonly string _descriptor;

  private SocketFileDescriptor(string descriptor)
    => _descriptor = descriptor;

  /// <inheritdoc />
  public override string ToString()
    => _descriptor;

  /// <summary>
  ///   Implicitly converts a <see cref="SocketFileDescriptor" /> to its underlying string representation.
  /// </summary>
  /// <param name="descriptor">The <see cref="SocketFileDescriptor" /> instance to convert.</param>
  /// <returns>The string representation of the socket file descriptor.</returns>
  public static implicit operator string(SocketFileDescriptor descriptor)
    => descriptor._descriptor;
}
