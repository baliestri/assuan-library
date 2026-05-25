// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Common.Transport;

internal sealed class TcpClientConnectionFactory(AssuanClientOptions options) : IAssuanConnectionFactory {
  /// <inheritdoc />
  public IAssuanConnection CreateConnection(IAssuanEndpoint endpoint) {
    if (endpoint is not TcpClientEndpoint tcp) {
      throw new NotSupportedException(
        $"The endpoint type '{endpoint.GetType().FullName}' is not supported by the TCP client connection factory.");
    }

    var connectionOptions = AssuanConnectionOptions.Default;
    options.ConfigureConnection?.Invoke(connectionOptions);

    return new TcpClientConnection(tcp, connectionOptions);
  }
}
