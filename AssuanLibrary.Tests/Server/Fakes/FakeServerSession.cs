// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Tests.Server.Fakes;

internal sealed class FakeServerSession : IServerSession {
  public FakeServerSession(CancellationToken cancellationToken) => CancellationToken = cancellationToken;

  public int RefreshCalls { get; private set; }

  public Guid Id { get; } = Guid.NewGuid();
  public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
  public DateTimeOffset LastActivityAt { get; private set; } = DateTimeOffset.UtcNow;
  public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();
  public CancellationToken CancellationToken { get; }

  public void RefreshLastActivity() {
    RefreshCalls++;
    LastActivityAt = DateTimeOffset.UtcNow;
  }

  public void Dispose() { }
}
