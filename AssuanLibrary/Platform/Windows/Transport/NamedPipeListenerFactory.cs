// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Windows.Transport;

internal sealed class NamedPipeListenerFactory(AssuanServerOptions options) : IAssuanListenerFactory {
  /// <inheritdoc />
  public IAssuanListener CreateListener(IAssuanEndpoint endpoint) {
    if (endpoint is not NamedPipeEndpoint namedPipe) {
      throw new NotSupportedException($"The endpoint type '{endpoint.GetType().FullName}' is not supported by the named pipe listener factory.");
    }

    var listenerOptions = AssuanListenerOptions.Default;
    options.ConfigureListener?.Invoke(listenerOptions);

    return new NamedPipeListener(namedPipe, listenerOptions);
  }
}
