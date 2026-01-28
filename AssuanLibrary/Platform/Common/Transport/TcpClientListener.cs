// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Common.Transport;

internal sealed class TcpClientListener(TcpClientEndpoint endpoint, AssuanListenerOptions options) : IAssuanListener {
  private bool _disposed;
  private TcpListener? _listener;

  /// <inheritdoc />
  public bool IsListening { get; private set; }

  /// <inheritdoc />
  public IAssuanEndpoint Endpoint { get; } = endpoint;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientListener));

    if (IsListening) {
      return new TcpClientConnection(endpoint, AssuanConnectionOptions.Default);
    }

    _listener ??= new TcpListener(endpoint.EndPoint);
    _listener.Start();
    var tcpClient = _listener.AcceptTcpClient();

    IsListening = true;

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    return new TcpClientConnection(tcpClient, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);
  }

  /// <inheritdoc />
  public async ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientListener));

    if (IsListening) {
      return new TcpClientConnection(endpoint, AssuanConnectionOptions.Default);
    }

    _listener ??= new TcpListener(endpoint.EndPoint);
    _listener.Start();
    var tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);

    IsListening = true;

    return new TcpClientConnection(tcpClient, endpoint, AssuanConnectionOptions.Default);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _listener?.Stop();
    _listener = null;
    IsListening = false;
    _disposed = true;
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    _listener?.Stop();
    _listener = null;
    IsListening = false;
    _disposed = true;

    await Task.CompletedTask.ConfigureAwait(false);
  }
}
