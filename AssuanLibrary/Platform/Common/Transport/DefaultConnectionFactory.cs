// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Common.Transport;

internal sealed class DefaultConnectionFactory(AssuanClientOptions options) : IAssuanConnectionFactory {
  /// <inheritdoc />
  public IAssuanConnection CreateConnection(IAssuanEndpoint endpoint) {
    var connectionOptions = AssuanConnectionOptions.Default;
    options.ConfigureConnection?.Invoke(connectionOptions);

    return endpoint switch {
      NamedPipeEndpoint namedPipe => new NamedPipeConnection(namedPipe, connectionOptions),
      TcpClientEndpoint tcp => new TcpClientConnection(tcp, connectionOptions),
      UnixDomainSocketEndpoint unix => new UnixDomainSocketConnection(unix, connectionOptions),
      var _ => throw new NotSupportedException(
        $"The endpoint type '{endpoint.GetType().FullName}' is not supported by the default connection factory.")
    };
  }
}
