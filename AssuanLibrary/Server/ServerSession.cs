// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Concurrent;
using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Server;

/// <summary>
///   Represents a session for an Assuan connection.
/// </summary>
/// <param name="ct">A <see cref="CancellationToken" /> to observe cancellation requests.</param>
public sealed class ServerSession(CancellationToken ct = default) : IServerSession {
  private readonly CancellationTokenSource _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
  private bool _isClosing;

  /// <inheritdoc />
  public Guid Id { get; } = Guid.CreateVersion7();

  /// <inheritdoc />
  public IDictionary<string, object?> Items { get; } = new ConcurrentDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

  /// <inheritdoc />
  public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

  /// <inheritdoc />
  public DateTimeOffset LastActivityAt { get; private set; } = DateTimeOffset.UtcNow;

  /// <inheritdoc />
  public CancellationToken CancellationToken => _cts.Token;

  /// <inheritdoc />
  public void RefreshLastActivity()
    => LastActivityAt = DateTimeOffset.UtcNow;

  /// <inheritdoc />
  public void CloseGracefully() {
    if (_isClosing) {
      return;
    }

    _isClosing = true;
    _cts.Cancel();
  }

  /// <inheritdoc />
  public void Dispose()
    => _cts.Dispose();
}
