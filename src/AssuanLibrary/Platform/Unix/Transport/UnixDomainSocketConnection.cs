// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Extensions;
using AssuanLibrary.Platform.Unix.Extensions;
using AssuanLibrary.Platform.Unix.Polyfills;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Platform.Unix.Transport.IO;
using AssuanLibrary.Polyfills;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Unix.Transport;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class UnixDomainSocketConnection : IAssuanConnection {
  private readonly UnixDomainSocketEndpoint _endpoint;
  private readonly AssuanConnectionOptions _options;
  private readonly StabilizationOptions _stabilizationOptions;
  private bool _disposed;
  private Socket? _socket;

  public UnixDomainSocketConnection(UnixDomainSocketEndpoint endpoint, AssuanConnectionOptions options) {
    _endpoint = endpoint;
    _options = options;
    _stabilizationOptions = StabilizationOptions.Default;
    _options.ConfigureStabilization?.Invoke(_stabilizationOptions);
  }

  public UnixDomainSocketConnection(Socket socket, UnixDomainSocketEndpoint endpoint, AssuanConnectionOptions options,
  StabilizationOptions? stabilizationOptions = null) {
    _socket = socket;
    _endpoint = endpoint;
    _options = options;
    _stabilizationOptions = stabilizationOptions ?? StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(_stabilizationOptions);
  }

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_socket))]
  public bool IsConnected => _socket is { Connected: true };

  /// <inheritdoc />
  public void Open() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (IsConnected) {
      return;
    }

    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified) {
      ReceiveTimeout = _options.TimeoutInMilliseconds,
      SendTimeout = _options.TimeoutInMilliseconds
    };

    socket.Connect(_endpoint);

    _socket = socket;
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    _socket.Send(buffer, SocketFlags.None);
  }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    using var reader = new StabilizedSocketReader(_socket, _options.TimeoutInMilliseconds, _stabilizationOptions);
    return reader.Read();
  }

  /// <inheritdoc />
  public byte[] Read(InquireHandler inquireHandler) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket client is not connected.");
    }

    return InquireReadLoop.Read(this, buffer => _socket.Receive(buffer, 0, 1, SocketFlags.None), inquireHandler);
  }

  /// <inheritdoc />
  public byte[] ReadAvailable() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    using var ms = new MemoryStream();
    var buffer = new byte[256];

    while (true) {
      var read = _socket.Receive(buffer, SocketFlags.None);

      if (read == 0) {
        break; // EOF
      }

      ms.Write(buffer.AsSpan(0, read).ToArray());

      if (buffer[read - 1] == Characters.LINE_FEED) {
        break;
      }
    }

    return ms.ToArray();
  }

  /// <inheritdoc />
  public void DiscardPendingInput() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    _socket.DiscardAvailableData();
  }

  /// <inheritdoc />
  public void Close() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    _socket.Shutdown(SocketShutdown.Both);
    _socket.Close();
  }

  /// <inheritdoc />
  public async Task OpenAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (IsConnected) {
      return;
    }

    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified) {
      ReceiveTimeout = _options.TimeoutInMilliseconds,
      SendTimeout = _options.TimeoutInMilliseconds
    };

    await socket.ConnectAsync(_endpoint).ConfigureAwait(false);

    _socket = socket;
  }

  /// <inheritdoc />
  public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    if (!MemoryMarshal.TryGetArray(buffer, out var arraySegment)) {
      arraySegment = new ArraySegment<byte>(buffer.ToArray());
    }

    await _socket.SendAsync(arraySegment, SocketFlags.None, ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    using var reader = new StabilizedSocketReader(_socket, _options.TimeoutInMilliseconds, _stabilizationOptions);
    return await reader.ReadAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler,
  CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket client is not connected.");
    }

    return InquireReadLoop.ReadAsync(this,
      (buffer, token) => new ValueTask<int>(_socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None, token)),
      inquireHandler, ct);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAvailableAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    using var ms = new MemoryStream();
    var buffer = new byte[256];
    var arraySegment = new ArraySegment<byte>(buffer);

    while (true) {
      ct.ThrowIfCancellationRequested();

      var read = await _socket.ReceiveAsync(arraySegment, SocketFlags.None, ct).ConfigureAwait(false);

      if (read == 0) {
        break; // EOF
      }

      ms.Write(buffer.AsSpan(0, read).ToArray());

      if (buffer[read - 1] == Characters.LINE_FEED) {
        break;
      }
    }

    return ms.ToArray();
  }

  /// <inheritdoc />
  public async ValueTask DiscardPendingInputAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    await _socket.DiscardAvailableDataAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task CloseAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    await Task.Run(() => {
      _socket.Shutdown(SocketShutdown.Both);
      _socket.Close();
    }, ct);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    try {
      _socket?.Dispose();
    }
    finally {
      _socket = null;
      _disposed = true;
    }
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    try {
      if (_socket is not null) {
        await CastAndDispose(_socket);
      }
    }
    finally {
      _socket = null;
      _disposed = true;
    }

    return;

    static async ValueTask CastAndDispose(IDisposable resource) {
      if (resource is IAsyncDisposable resourceAsyncDisposable) {
        await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
        return;
      }

      resource.Dispose();
    }
  }
}
