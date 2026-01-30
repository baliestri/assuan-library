// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Unix.Transport;

internal sealed class UnixDomainSocketListenerFactory(AssuanServerOptions options) : IAssuanListenerFactory {
  /// <inheritdoc />
  public IAssuanListener CreateListener(IAssuanEndpoint endpoint) {
    if (endpoint is not UnixDomainSocketEndpoint unix) {
      throw new NotSupportedException(
        $"The endpoint type '{endpoint.GetType().FullName}' is not supported by the Unix domain socket listener factory.");
    }

    var listenerOptions = AssuanListenerOptions.Default;
    options.ConfigureListener?.Invoke(listenerOptions);

    return new UnixDomainSocketListener(unix, listenerOptions);
  }
}
