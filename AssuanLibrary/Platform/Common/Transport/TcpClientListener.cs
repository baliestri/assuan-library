// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Common.Transport;

internal sealed class TcpClientListener(TcpClientEndpoint endpoint, AssuanListenerOptions options) : IAssuanListener {
  /// <inheritdoc />
  public IAssuanEndpoint Endpoint { get; } = endpoint;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    var currentListener = new TcpListener(endpoint.EndPoint);
    currentListener.Start();

    var tcpClient = currentListener.AcceptTcpClient();

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    return new TcpClientConnection(tcpClient, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);
  }

  /// <inheritdoc />
  public async ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
    var currentListener = new TcpListener(endpoint.EndPoint);
    currentListener.Start();

    var tcpClient = await currentListener.AcceptTcpClientAsync().ConfigureAwait(false);

    return new TcpClientConnection(tcpClient, endpoint, AssuanConnectionOptions.Default);
  }
}
