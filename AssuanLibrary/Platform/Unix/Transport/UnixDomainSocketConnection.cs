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
using AssuanLibrary.Platform.Unix.Endpoints;
using AssuanLibrary.Platform.Unix.Extensions;
using AssuanLibrary.Platform.Unix.Polyfills;
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

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_socket))]
  public bool IsConnected => _socket is { Connected: true };

  /// <inheritdoc />
  public void Open() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    var polyfillEndpoint = new UnixDomainSocketEndPoint(_endpoint.Path);
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified) {
      ReceiveTimeout = (int)_options.Timeout.TotalMilliseconds,
      SendTimeout = (int)_options.Timeout.TotalMilliseconds
    };

    socket.Connect(polyfillEndpoint);
    socket.DiscardAvailableData();

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

    using var reader = new StabilizedSocketReader(_socket, _options.Timeout, _stabilizationOptions);
    return reader.Read();
  }

  /// <inheritdoc />
  public byte[] Read(InquireHandler inquireHandler) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();

    var b = new byte[1];

    while (true) {
      var bytesRead = _socket.Receive(b, 0, 1, SocketFlags.None);
      if (bytesRead < 0) {
        break; // EOF
      }

      memoryStream.WriteByte(b[0]);

      if (b[0] != Characters.LINE_FEED) {
        continue;
      }

      var responseBuffer = memoryStream.ToArray();
      var response = new AssuanResponse(responseBuffer.Take(Characters.LINE_FEED));

      finalMemoryStream.Write(responseBuffer);
      memoryStream.SetLength(0);

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new InquireContext(this, keyword, parameters);

      try {
        inquireHandler(ctx);
      }
      catch {
        ctx.Cancel();
        throw;
      }
    }

    return finalMemoryStream.ToArray();
  }

  /// <inheritdoc />
  public void Close() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    var buffer = "BYE\n"u8.ToArray();
    _socket.Send(buffer, SocketFlags.None);
    _socket.DiscardAvailableData();
    _socket.Shutdown(SocketShutdown.Both);
  }

  /// <inheritdoc />
  public async Task OpenAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    var polyfillEndpoint = new UnixDomainSocketEndPoint(_endpoint.Path);
    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified) {
      ReceiveTimeout = (int)_options.Timeout.TotalMilliseconds,
      SendTimeout = (int)_options.Timeout.TotalMilliseconds
    };

    await socket.ConnectAsync(polyfillEndpoint);
    await socket.DiscardAvailableDataAsync(ct);

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

    await _socket.SendAsync(arraySegment, SocketFlags.None, ct);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    using var reader = new StabilizedSocketReader(_socket, _options.Timeout, _stabilizationOptions);
    return await reader.ReadAsync(ct);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler,
  CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();

    var b = new byte[1];
    var segment = new ArraySegment<byte>(b);

    while (true) {
      var bytesRead = await _socket.ReceiveAsync(segment, SocketFlags.None, ct);
      if (bytesRead < 0) {
        break; // EOF
      }

      memoryStream.WriteByte(b[0]);

      if (b[0] != Characters.LINE_FEED) {
        continue;
      }

      var responseBuffer = memoryStream.ToArray();
      var response = new AssuanResponse(responseBuffer.Take(Characters.LINE_FEED));

      finalMemoryStream.Write(responseBuffer);
      memoryStream.SetLength(0);

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new InquireContext(this, keyword, parameters);

      try {
        await inquireHandler(ctx, ct);
      }
      catch {
        await ctx.CancelAsync(ct);
        throw;
      }
    }

    return finalMemoryStream.ToArray();
  }

  /// <inheritdoc />
  public async Task CloseAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(UnixDomainSocketConnection));

    if (!IsConnected) {
      throw new AssuanClientException("Socket is not connected.");
    }

    var buffer = "BYE\n"u8.ToArray();
    var segment = new ArraySegment<byte>(buffer);
    await _socket.SendAsync(segment, SocketFlags.None, ct);
    await _socket.DiscardAvailableDataAsync(ct);
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
