// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Common.Transport;

internal sealed class TcpClientListenerFactory(AssuanServerOptions options) : IAssuanListenerFactory {
  /// <inheritdoc />
  public IAssuanListener CreateListener(IAssuanEndpoint endpoint) {
    if (endpoint is not TcpClientEndpoint tcp) {
      throw new NotSupportedException($"The endpoint type '{endpoint.GetType().FullName}' is not supported by the TCP client listener factory.");
    }

    var listenerOptions = AssuanListenerOptions.Default;
    options.ConfigureListener?.Invoke(listenerOptions);

    return new TcpClientListener(tcp, listenerOptions);
  }
}
