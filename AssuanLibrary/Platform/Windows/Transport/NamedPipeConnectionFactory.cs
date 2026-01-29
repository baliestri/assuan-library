// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Windows.Transport;

internal sealed class NamedPipeConnectionFactory(AssuanClientOptions options) : IAssuanConnectionFactory {
  /// <inheritdoc />
  public IAssuanConnection CreateConnection(IAssuanEndpoint endpoint) {
    if (endpoint is not NamedPipeEndpoint namedPipe) {
      throw new NotSupportedException($"The endpoint type '{endpoint.GetType().FullName}' is not supported by the named pipe connection factory.");
    }

    var connectionOptions = AssuanConnectionOptions.Default;
    options.ConfigureConnection?.Invoke(connectionOptions);

    return new NamedPipeConnection(namedPipe, connectionOptions);
  }
}
