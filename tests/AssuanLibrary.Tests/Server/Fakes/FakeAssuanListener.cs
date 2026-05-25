// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Tests.Server.Fakes;

internal sealed class FakeAssuanListener : IAssuanListener {
  public FakeAssuanListener(IAssuanEndpoint endpoint) => Endpoint = endpoint;

  public Queue<IAssuanConnection> AcceptQueue { get; } = new();

  public IAssuanEndpoint Endpoint { get; }

  public IAssuanConnection Accept() {
    if (AcceptQueue.Count == 0) {
      throw new InvalidOperationException("No connection enqueued.");
    }

    return AcceptQueue.Dequeue();
  }

  public ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default)
    => ValueTask.FromResult(Accept());
}
