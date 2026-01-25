// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Endpoints;

namespace AssuanLibrary.Platform.Windows.Endpoints;

/// <summary>
///   Defines a TCP client communication endpoint for Assuan protocol.
/// </summary>
/// <param name="EndPoint">The IP endpoint of the TCP client.</param>
/// <param name="Nonce">The nonce used for authentication.</param>
public readonly record struct TcpClientEndpoint(IPEndPoint EndPoint, ReadOnlyMemory<byte> Nonce) : IAssuanEndpoint;
