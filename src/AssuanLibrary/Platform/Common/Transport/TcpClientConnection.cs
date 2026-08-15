// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Extensions;
using AssuanLibrary.Platform.Common.Extensions;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Platform.Common.Transport.IO;
using AssuanLibrary.Polyfills;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Common.Transport;

internal sealed class TcpClientConnection : IAssuanConnection {
  private readonly TcpClientEndpoint _endpoint;
  private readonly AssuanConnectionOptions _options;
  private readonly StabilizationOptions _stabilizationOptions;
  private bool _disposed;
  private NetworkStream? _networkStream;
  private TcpClient? _tcpClient;

  public TcpClientConnection(TcpClientEndpoint endpoint, AssuanConnectionOptions options) {
    _endpoint = endpoint;
    _options = options;
    _stabilizationOptions = StabilizationOptions.Default;
    _options.ConfigureStabilization?.Invoke(_stabilizationOptions);
  }

  public TcpClientConnection(TcpClient tcpClient, TcpClientEndpoint endpoint, AssuanConnectionOptions options,
  StabilizationOptions? stabilizationOptions = null) {
    _endpoint = endpoint;
    _options = options;
    _stabilizationOptions = stabilizationOptions ?? StabilizationOptions.Default;
    _options.ConfigureStabilization?.Invoke(_stabilizationOptions);
    _tcpClient = tcpClient;
    _networkStream = tcpClient.GetStream();
  }

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_networkStream), nameof(_tcpClient))]
  public bool IsConnected => _tcpClient is { Connected: true };

  /// <inheritdoc />
  public void Open() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (IsConnected) {
      return;
    }

    var tcpClient = new TcpClient();

    tcpClient.Connect(_endpoint);
    var networkStream = tcpClient.GetStream();

    _tcpClient = tcpClient;
    _networkStream = networkStream;
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    _networkStream.Write(buffer);
    _networkStream.Flush();
  }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var reader = new StabilizedTcpClientReader(_tcpClient, _options.TimeoutInMilliseconds, _stabilizationOptions);
    return reader.Read();
  }

  /// <inheritdoc />
  public byte[] Read(InquireHandler inquireHandler) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();

    while (true) {
      var b = _networkStream.ReadByte();
      if (b < 0) {
        break; // EOF
      }

      memoryStream.WriteByte((byte)b);

      if (b != Characters.LINE_FEED) {
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

      var ctx = new ClientInquireContext(this, keyword, parameters);

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
  public byte[] ReadAvailable() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var ms = new MemoryStream();
    Span<byte> buffer = stackalloc byte[256];

    while (true) {
      var read = _networkStream.Read(buffer);

      if (read == 0) {
        break; // EOF
      }

      ms.Write(buffer[..read].ToArray());

      if (buffer[read - 1] == Characters.LINE_FEED) {
        break;
      }
    }

    return ms.ToArray();
  }

  /// <inheritdoc />
  public void DiscardPendingInput() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    _tcpClient.DiscardAvailableData();
  }

  /// <inheritdoc />
  public void Close() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    _networkStream.Close();
    _tcpClient.Close();
  }

  /// <inheritdoc />
  public async Task OpenAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (IsConnected) {
      return;
    }

    var tcpClient = new TcpClient();

    await tcpClient.ConnectAsync(_endpoint, ct).ConfigureAwait(false);
    var networkStream = tcpClient.GetStream();

    _tcpClient = tcpClient;
    _networkStream = networkStream;
  }

  /// <inheritdoc />
  public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    await _networkStream.WriteAsync(buffer, ct).ConfigureAwait(false);
    await _networkStream.FlushAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var reader = new StabilizedTcpClientReader(_tcpClient, _options.TimeoutInMilliseconds, _stabilizationOptions);
    return await reader.ReadAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler,
  CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();

    var b = new byte[1];

    while (true) {
      var bytesRead = await _networkStream.ReadAsync(b, ct).ConfigureAwait(false);
      if (bytesRead == 0) {
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

      var ctx = new ClientInquireContext(this, keyword, parameters);

      try {
        await inquireHandler(ctx, ct);
      }
      catch {
        await ctx.CancelAsync(ct).ConfigureAwait(false);
        throw;
      }
    }

    return finalMemoryStream.ToArray();
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAvailableAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    using var ms = new MemoryStream();
    using var memoryOwner = MemoryPool<byte>.Shared.Rent(256);
    var memory = memoryOwner.Memory;

    while (true) {
      ct.ThrowIfCancellationRequested();

      var read = await _networkStream.ReadAsync(memory, ct).ConfigureAwait(false);

      if (read == 0) {
        break; // EOF
      }

      ms.Write(memory[..read]);

      if (memoryOwner.Memory.Span[read - 1] == Characters.LINE_FEED) {
        break;
      }
    }

    return ms.ToArray();
  }

  /// <inheritdoc />
  public async ValueTask DiscardPendingInputAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    await _tcpClient.DiscardAvailableDataAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task CloseAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    await Task.Run(() => {
      _networkStream.Close();
      _tcpClient.Close();
    }, ct);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    try {
      _networkStream?.Dispose();
      _tcpClient?.Dispose();
    }
    finally {
      _networkStream = null;
      _tcpClient = null;
      _disposed = true;
    }
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    try {
      if (_networkStream is not null) {
        await CastAndDispose(_networkStream);
      }

      if (_tcpClient is not null) {
        await CastAndDispose(_tcpClient);
      }
    }
    finally {
      _networkStream = null;
      _tcpClient = null;
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
