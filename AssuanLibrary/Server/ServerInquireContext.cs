// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Server;

internal sealed class ServerInquireContext : IServerInquireContext {
  private readonly TaskCompletionSource<ReadOnlyMemory<byte>> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private CancellationTokenRegistration _ctr;
  private bool _disposed;

  public ServerInquireContext(string keyword, IReadOnlyCollection<string> parameters, CancellationToken ct = default) {
    Keyword = keyword;
    Parameters = parameters;
    _ctr = ct.Register(() => _tcs.TrySetCanceled(ct));
  }

  /// <inheritdoc />
  public string Keyword { get; }

  /// <inheritdoc />
  public IReadOnlyCollection<string> Parameters { get; }

  /// <inheritdoc />
  public byte[] Wait() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    return _tcs.Task.GetAwaiter().GetResult().ToArray();
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> WaitAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
    return await _tcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    _ctr.Dispose();
  }

  /// <summary>
  ///   Completes the inquire request with the specified data.
  /// </summary>
  /// <param name="data">The data to complete the inquire request with.</param>
  public void Complete(ReadOnlyMemory<byte> data) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    _tcs.TrySetResult(data);
  }

  /// <summary>
  ///   Faults the inquire request with the specified exception.
  /// </summary>
  /// <param name="ex">The exception to fault the inquire request with.</param>
  public void Fault(Exception ex) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    _tcs.TrySetException(ex);
  }

  /// <summary>
  ///   Cancels the inquire request.
  /// </summary>
  public void Cancel() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    _tcs.TrySetCanceled();
  }
}
