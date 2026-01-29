// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Common.Transport.Endpoints;

/// <summary>
///   Defines a TCP client communication endpoint for Assuan protocol.
/// </summary>
/// <param name="EndPoint">The IP endpoint of the TCP client.</param>
public readonly record struct TcpClientEndpoint(IPEndPoint EndPoint) : IAssuanEndpoint {
  /// <summary>
  ///   Initializes a new instance of the <see cref="TcpClientEndpoint" /> class with the specified IP address and port.
  /// </summary>
  /// <param name="ipAddress">The IP address of the TCP client.</param>
  /// <param name="port">The port number of the TCP client.</param>
  public TcpClientEndpoint(IPAddress ipAddress, ushort port) : this(new IPEndPoint(ipAddress, port)) { }

  /// <summary>
  ///   Implicitly converts a <see cref="TcpClientEndpoint" /> to an <see cref="IPEndPoint" />.
  /// </summary>
  /// <param name="endpoint">The TCP client endpoint to convert.</param>
  /// <returns>The corresponding IP endpoint.</returns>
  public static implicit operator IPEndPoint(TcpClientEndpoint endpoint)
    => endpoint.EndPoint;
}
