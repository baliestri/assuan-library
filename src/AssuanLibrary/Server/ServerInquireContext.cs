// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Server;

/// <summary>
///   Represents the context for handling an inquire request from the client.
/// </summary>
public sealed class ServerInquireContext : IServerInquireContext {
  private readonly MemoryStream _memoryStream;
  private readonly CancellationToken _sessionToken;
  private readonly TaskCompletionSource<ReadOnlyMemory<byte>> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private bool _completed;
  private CancellationTokenRegistration _ctr;
  private bool _disposed;

  /// <summary>
  ///   Initializes a new instance of the <see cref="ServerInquireContext" /> class.
  /// </summary>
  /// <param name="keyword">The inquire keyword.</param>
  /// <param name="parameters">The inquire parameters.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the inquire request to complete.</param>
  public ServerInquireContext(string keyword, IReadOnlyCollection<string> parameters, CancellationToken ct = default) {
    Keyword = keyword;
    Parameters = parameters;
    _sessionToken = ct;
    _ctr = ct.Register(() => _tcs.TrySetCanceled(ct));
    _memoryStream = new MemoryStream();
  }

  /// <inheritdoc />
  public string Keyword { get; }

  /// <inheritdoc />
  public IReadOnlyCollection<string> Parameters { get; }

  /// <inheritdoc />
  public void Receive(ReadOnlySpan<byte> buffer) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    if (_completed) {
      throw new InvalidOperationException("Cannot receive data after the inquire context has been completed.");
    }

    _memoryStream.Write(buffer.ToArray(), 0, buffer.Length);
  }

  /// <inheritdoc />
  public void Complete() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    if (_completed) {
      return;
    }

    _completed = true;
    _tcs.TrySetResult(_memoryStream.ToArray());
  }

  /// <inheritdoc />
  public void Cancel() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    if (_completed) {
      return;
    }

    _tcs.TrySetCanceled();
  }

  /// <inheritdoc />
  public byte[] Wait() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    return _tcs.Task.GetAwaiter().GetResult().ToArray();
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> WaitAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(ServerInquireContext));

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(_sessionToken, ct);
    return await _tcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _ctr.Dispose();
    _memoryStream.Dispose();
    _disposed = true;
  }
}
