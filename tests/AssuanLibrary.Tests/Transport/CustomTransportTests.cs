// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Transport;

public sealed class CustomTransportTests {
  [Fact]
  public async Task CustomAcceptorNeedsNoInventedTcpEndpointOrPeerIdentity() {
    await using IAssuanAcceptor acceptor = new OneShotAcceptor(new MemoryStream());
    var accepted = await acceptor.AcceptAsync(CancellationToken.None);
    Assert.Null(accepted.PeerIdentity);
    await using var stream = accepted.Stream;
    Assert.True(stream is { CanRead: true, CanWrite: true });
  }

  private sealed class OneShotAcceptor(Stream stream) : IAssuanAcceptor {
    private Stream? _stream = stream;

    public ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult(new AcceptedConnection(
        Interlocked.Exchange(ref _stream, null) ?? throw new InvalidOperationException("Already accepted.")));
    }

    public ValueTask DisposeAsync() {
      Interlocked.Exchange(ref _stream, null)?.Dispose();
      return ValueTask.CompletedTask;
    }
  }
}
