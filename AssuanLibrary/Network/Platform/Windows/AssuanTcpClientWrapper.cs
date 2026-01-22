// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Extensions;
using AssuanLibrary.Network.Utility;

namespace AssuanLibrary.Network.Platform.Windows;

internal sealed class AssuanTcpClientWrapper(SocketDescriptor socketDescriptor, TimeSpan timeout) : IAssuanClientWrapper {
  private readonly PortAndNonce _portAndNonce = SocketFileReader.Get(socketDescriptor);
  private bool _disposed;
  private NetworkStream? _networkStream;
  private TcpClient? _tcpClient;

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_networkStream), nameof(_tcpClient))]
  public bool IsConnected => _tcpClient is { Connected: true };

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    if (IsConnected) {
      await DisconnectAsync();
    }

    if (_networkStream is not null) {
      await CastAndDispose(_networkStream);
    }

    if (_tcpClient is not null) {
      await CastAndDispose(_tcpClient);
    }

    _networkStream = null;
    _tcpClient = null;
    _disposed = true;

    return;

    static async ValueTask CastAndDispose(IDisposable resource) {
      if (resource is IAsyncDisposable resourceAsyncDisposable) {
        await resourceAsyncDisposable.DisposeAsync();
        return;
      }

      resource.Dispose();
    }
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    if (IsConnected) {
      Disconnect();
    }

    _networkStream?.Dispose();
    _tcpClient?.Dispose();
    _networkStream = null;
    _tcpClient = null;
    _disposed = true;
  }

  /// <inheritdoc />
  public void Connect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    var (port, nonce) = _portAndNonce;
    var tcpClient = new TcpClient();

    tcpClient.Connect(IPAddress.Loopback, port);
    var networkStream = tcpClient.GetStream();

    networkStream.Write(nonce);
    networkStream.Flush();
    tcpClient.DiscardAvailableData();

    _tcpClient = tcpClient;
    _networkStream = networkStream;
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    _networkStream.Write(buffer);
    _networkStream.Flush();
  }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var reader = new StabilizedStreamReader(_tcpClient, timeout);
    return reader.Read();
  }

  /// <inheritdoc />
  public void Disconnect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    _networkStream.Write("BYE\n"u8.ToArray());
    _networkStream.Flush();
    _tcpClient.DiscardAvailableData();
  }

  /// <inheritdoc />
  public async Task ConnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    var (port, nonce) = _portAndNonce;
    var tcpClient = new TcpClient();

    await tcpClient.ConnectAsync(IPAddress.Loopback, port);
    var networkStream = tcpClient.GetStream();

    await networkStream.WriteAsync(nonce, ct);
    await networkStream.FlushAsync(ct);
    await tcpClient.DiscardAvailableDataAsync(ct);

    _tcpClient = tcpClient;
    _networkStream = networkStream;
  }

  /// <inheritdoc />
  public async Task WriteAsync(byte[] buffer, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    await _networkStream.WriteAsync(buffer, ct);
    await _networkStream.FlushAsync(ct);
  }

  /// <inheritdoc />
  public async ValueTask<byte[]> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var reader = new StabilizedStreamReader(_tcpClient, timeout);
    return await reader.ReadAsync(ct);
  }

  /// <inheritdoc />
  public async Task DisconnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    var buffer = "BYE\n"u8.ToArray();

    await _networkStream.WriteAsync(buffer, ct);
    await _networkStream.FlushAsync(ct);
    await _tcpClient.DiscardAvailableDataAsync(ct);
  }
}
