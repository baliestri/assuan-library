// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.Versioning;
using AssuanLibrary.Extensions;
using AssuanLibrary.Network.Utility;

namespace AssuanLibrary.Network.Platform.Unix;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class AssuanSocketClientWrapper(SocketDescriptor socketDescriptor, TimeSpan timeout) : IAssuanClientWrapper {
  private readonly string _socketPath = SocketFileReader.GetSocketPath(socketDescriptor);
  private bool _disposed;
  private Socket? _socket;

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_socket))]
  public bool IsConnected => _socket is { Connected: true };

  /// <inheritdoc />
  public void Connect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    var endpoint = new UnixDomainSocketEndPoint(_socketPath);
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified) {
      ReceiveTimeout = (int)timeout.TotalMilliseconds,
      SendTimeout = (int)timeout.TotalMilliseconds
    };

    socket.Connect(endpoint);
    socket.DiscardAvailableData();

    _socket = socket;
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    _socket.Send(buffer, SocketFlags.None);
  }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    using var reader = new StabilizedSocketReader(_socket, timeout);
    return reader.Read();
  }

  /// <inheritdoc />
  public void Disconnect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    var buffer = "BYE\n"u8.ToArray();
    _socket.Send(buffer, SocketFlags.None);
    _socket.Shutdown(SocketShutdown.Both);
  }

  /// <inheritdoc />
  public async Task ConnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    var endpoint = new UnixDomainSocketEndPoint(_socketPath);
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified) {
      ReceiveTimeout = (int)timeout.TotalMilliseconds,
      SendTimeout = (int)timeout.TotalMilliseconds
    };

    await socket.ConnectAsync(endpoint);
    await socket.DiscardAvailableDataAsync();

    _socket = socket;
  }

  /// <inheritdoc />
  public async Task WriteAsync(byte[] buffer, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    var segment = new ArraySegment<byte>(buffer);
    await _socket.SendAsync(segment, SocketFlags.None);
  }

  /// <inheritdoc />
  public async ValueTask<byte[]> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    using var reader = new StabilizedSocketReader(_socket, timeout);
    return await reader.ReadAsync(ct);
  }

  /// <inheritdoc />
  public async Task DisconnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanSocketClientWrapper));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    var buffer = "BYE\n"u8.ToArray();
    var segment = new ArraySegment<byte>(buffer);
    await _socket.SendAsync(segment, SocketFlags.None);
    _socket.Shutdown(SocketShutdown.Both);
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

    if (_socket is not null) {
      await CastAndDispose(_socket);
    }

    _socket = null;
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
}
