// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Endpoints;
using AssuanLibrary.Platform.Common.Endpoints;
using AssuanLibrary.Platform.Unix.Endpoints;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Platform.Common.Transport;

internal sealed class DefaultConnectionFactory(AssuanClientOptions options) : IAssuanConnectionFactory {
  /// <inheritdoc />
  public IAssuanConnection Create(IAssuanEndpoint endpoint)
    => endpoint switch {
      UnixDomainSocketEndpoint unix => new UnixDomainSocketConnection(unix, options.ConnectionOptions),
      TcpClientEndpoint tcp => new TcpClientConnection(tcp, options.ConnectionOptions),
      var _ => throw new NotSupportedException(
        $"The endpoint type '{endpoint.GetType().FullName}' is not supported by the default connection factory.")
    };
}
