// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using AssuanLibrary.Extensions;

namespace AssuanLibrary.Network;

internal sealed class AssuanTcpClientWrapper(TimeSpan timeout) : IAsyncDisposable, IDisposable {
  private bool _disposed;
  private NetworkStream? _networkStream;
  private TcpClient? _tcpClient;

  /// <summary>
  ///   Indicates whether the TCP client is currently connected.
  /// </summary>
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

  /// <summary>
  ///   Connects to the specified IP address and port using the provided nonce for authentication.
  /// </summary>
  /// <param name="ipAddress">The IP address to connect to.</param>
  /// <param name="portAndNonce">The port and nonce information to use when connecting.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  public async Task ConnectAsync(IPAddress ipAddress, PortAndNonce portAndNonce, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    var (port, nonce) = portAndNonce;
    var tcpClient = new TcpClient();

    await tcpClient.ConnectAsync(ipAddress, port);
    var networkStream = tcpClient.GetStream();

    await networkStream.WriteAsync(nonce, ct);
    await networkStream.FlushAsync(ct);
    await tcpClient.DiscardAvailableDataAsync(ct);

    _tcpClient = tcpClient;
    _networkStream = networkStream;
  }

  /// <summary>
  ///   Writes data to the TCP client.
  /// </summary>
  /// <param name="buffer">The data to write.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <exception cref="AssuanTcpClientException">Thrown when the TCP client is not connected.</exception>
  public async Task WriteAsync(byte[] buffer, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanTcpClientException("TCP client is not connected.");
    }

    await _networkStream.WriteAsync(buffer, ct);
    await _networkStream.FlushAsync(ct);
  }

  /// <summary>
  ///   Reads data from the TCP client asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous read operation, containing the data read.</returns>
  /// <exception cref="AssuanTcpClientException">Thrown when the TCP client is not connected.</exception>
  public async ValueTask<byte[]> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanTcpClientException("TCP client is not connected.");
    }

    using var reader = new StabilizedStreamReader(_tcpClient, timeout);
    return await reader.ReadAsync(ct);
  }

  /// <summary>
  ///   Disconnects from the TCP client gracefully.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <exception cref="AssuanTcpClientException">Thrown when the TCP client is not connected.</exception>
  public async Task DisconnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanTcpClientException("TCP client is not connected.");
    }

    var buffer = "BYE\n"u8.ToArray();

    await _networkStream.WriteAsync(buffer, ct);
    await _networkStream.FlushAsync(ct);
    await _tcpClient.DiscardAvailableDataAsync(ct);
  }

  /// <summary>
  ///   Connects to the specified IP address and port using the provided nonce for authentication.
  /// </summary>
  /// <param name="ipAddress">The IP address to connect to.</param>
  /// <param name="portAndNonce">The port and nonce information to use when connecting.</param>
  public void Connect(IPAddress ipAddress, PortAndNonce portAndNonce) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    var (port, nonce) = portAndNonce;
    var tcpClient = new TcpClient();

    tcpClient.Connect(ipAddress, port);
    var networkStream = tcpClient.GetStream();

    networkStream.Write(nonce);
    networkStream.Flush();
    tcpClient.DiscardAvailableData();

    _tcpClient = tcpClient;
    _networkStream = networkStream;
  }

  /// <summary>
  ///   Writes data to the TCP client.
  /// </summary>
  /// <param name="buffer">The data to write.</param>
  /// <exception cref="AssuanTcpClientException">Thrown when the TCP client is not connected.</exception>
  public void Write(byte[] buffer) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanTcpClientException("TCP client is not connected.");
    }

    _networkStream.Write(buffer);
    _networkStream.Flush();
  }

  /// <summary>
  ///   Reads data from the TCP client.
  /// </summary>
  /// <returns>An enumerable of read-only memory segments containing the data read.</returns>
  /// <exception cref="AssuanTcpClientException">Thrown when the TCP client is not connected.</exception>
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanTcpClientException("TCP client is not connected.");
    }

    using var reader = new StabilizedStreamReader(_tcpClient, timeout);
    return reader.Read();
  }

  /// <summary>
  ///   Disconnects from the TCP client gracefully.
  /// </summary>
  /// <exception cref="AssuanTcpClientException">Thrown when the TCP client is not connected.</exception>
  public void Disconnect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanTcpClientWrapper));

    if (!IsConnected) {
      throw new AssuanTcpClientException("TCP client is not connected.");
    }

    _networkStream.Write("BYE\n"u8.ToArray());
    _networkStream.Flush();
    _tcpClient.DiscardAvailableData();
  }
}
