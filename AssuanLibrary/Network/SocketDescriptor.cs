// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Network;

/// <summary>
///   Represents a smart enum for which socket file to use.
/// </summary>
public sealed class SocketDescriptor {
  /// <summary>
  ///   Uses <c>agent-socket</c> as the socket descriptor.
  /// </summary>
  public static readonly SocketDescriptor AgentSocket = new("agent-socket");

  /// <summary>
  ///   Uses <c>dirmngr-socket</c> as the socket descriptor.
  /// </summary>
  public static readonly SocketDescriptor DirmngrSocket = new("dirmngr-socket");

  /// <summary>
  ///   Uses <c>keyboxd-socket</c> as the socket descriptor.
  /// </summary>
  public static readonly SocketDescriptor KeyboxdSocket = new("keyboxd-socket");

  private readonly string _descriptor;

  private SocketDescriptor(string descriptor)
    => _descriptor = descriptor;

  /// <inheritdoc />
  public override string ToString()
    => _descriptor;

  /// <summary>
  ///   Implicitly converts a <see cref="SocketDescriptor" /> to its underlying string representation.
  /// </summary>
  /// <param name="descriptor">The <see cref="SocketDescriptor" /> instance to convert.</param>
  /// <returns>The string representation of the socket descriptor.</returns>
  public static implicit operator string(SocketDescriptor descriptor)
    => descriptor._descriptor;
}
