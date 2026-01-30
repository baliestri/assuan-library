// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Unix.Transport;

internal sealed class UnixDomainSocketConnectionFactory(AssuanClientOptions options) : IAssuanConnectionFactory {
  /// <inheritdoc />
  public IAssuanConnection CreateConnection(IAssuanEndpoint endpoint) {
    if (endpoint is not UnixDomainSocketEndpoint unix) {
      throw new NotSupportedException(
        $"The endpoint type '{endpoint.GetType().FullName}' is not supported by the Unix domain socket connection factory.");
    }

    var connectionOptions = AssuanConnectionOptions.Default;
    options.ConfigureConnection?.Invoke(connectionOptions);

    return new UnixDomainSocketConnection(unix, connectionOptions);
  }
}
