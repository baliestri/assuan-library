// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Unix.Transport;

internal sealed class UnixDomainSocketListener(UnixDomainSocketEndpoint endpoint, AssuanListenerOptions options) : IAssuanListener {
  private bool _disposed;
  private Socket? _socket;

  /// <inheritdoc />
  public bool IsListening { get; private set; }

  /// <inheritdoc />
  public IAssuanEndpoint Endpoint { get; } = endpoint;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketListener));

    if (IsListening) {
      return new UnixDomainSocketConnection(endpoint, AssuanConnectionOptions.Default);
    }

    _socket ??= new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    _socket.Bind(endpoint);
    _socket.Listen(1);
    var socket = _socket.Accept();

    IsListening = true;

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    return new UnixDomainSocketConnection(socket, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);
  }

  /// <inheritdoc />
  public async ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketListener));

    if (IsListening) {
      return new UnixDomainSocketConnection(endpoint, AssuanConnectionOptions.Default);
    }

    _socket ??= new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    _socket.Bind(endpoint);
    _socket.Listen(1);
    var socket = await _socket.AcceptAsync().ConfigureAwait(false);

    IsListening = true;

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    return new UnixDomainSocketConnection(socket, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _socket?.Dispose();
    _socket = null;
    _disposed = true;
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    _socket?.Dispose();
    _socket = null;
    _disposed = true;

    await Task.CompletedTask.ConfigureAwait(false);
  }
}
