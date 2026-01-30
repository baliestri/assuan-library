// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Tests.Server.Fakes;

internal sealed class FakeAssuanListenerFactory : IAssuanListenerFactory {
  public IAssuanEndpoint? LastEndpoint { get; private set; }

  public FakeAssuanListener Listener { get; private set; } = null!;

  public IAssuanListener CreateListener(IAssuanEndpoint endpoint) {
    LastEndpoint = endpoint;
    Listener = new FakeAssuanListener(endpoint);
    return Listener;
  }
}
