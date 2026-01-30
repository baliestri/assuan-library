// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Sample;

public sealed class ConsoleListenerFactory : IAssuanListenerFactory {
  /// <inheritdoc />
  public IAssuanListener CreateListener(IAssuanEndpoint endpoint) {
    if (endpoint is not ConsoleEndpoint) {
      throw new ArgumentException("The provided endpoint is not a ConsoleEndpoint.", nameof(endpoint));
    }

    return new ConsoleListener();
  }
}
