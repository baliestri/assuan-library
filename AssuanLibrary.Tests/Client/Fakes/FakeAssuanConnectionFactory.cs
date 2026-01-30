// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Tests.Client.Fakes;

internal sealed class FakeAssuanConnectionFactory : IAssuanConnectionFactory {
  public IAssuanEndpoint? LastEndpoint { get; private set; }

  public FakeAssuanConnection Connection { get; } = new();

  public IAssuanConnection CreateConnection(IAssuanEndpoint endpoint) {
    LastEndpoint = endpoint;
    return Connection;
  }
}
