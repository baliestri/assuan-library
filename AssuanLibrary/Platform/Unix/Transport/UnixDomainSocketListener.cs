// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Unix.Transport;

internal sealed class UnixDomainSocketListener(UnixDomainSocketEndpoint endpoint, AssuanListenerOptions options) : IAssuanListener {
  /// <inheritdoc />
  public IAssuanEndpoint Endpoint { get; } = endpoint;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    var currentSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    currentSocket.Bind(endpoint);
    currentSocket.Listen(1);

    var socket = currentSocket.Accept();

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    return new UnixDomainSocketConnection(socket, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);
  }

  /// <inheritdoc />
  public async ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
    var currentSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    currentSocket.Bind(endpoint);
    currentSocket.Listen(1);

    var socket = await currentSocket.AcceptAsync().ConfigureAwait(false);

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    return new UnixDomainSocketConnection(socket, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);
  }
}
