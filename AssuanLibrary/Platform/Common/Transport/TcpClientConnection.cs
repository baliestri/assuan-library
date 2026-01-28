// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

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

    var (ipEndPoint, nonce) = _endpoint;
    var tcpClient = new TcpClient();

    tcpClient.Connect(ipEndPoint);
    var networkStream = tcpClient.GetStream();

    networkStream.Write(nonce);
    networkStream.Flush();
    tcpClient.DiscardAvailableData();

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
  public void Close() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    _networkStream.Write(Commands.Bye);
    _networkStream.Flush();
    _tcpClient.DiscardAvailableData();
  }

  /// <inheritdoc />
  public async Task OpenAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (IsConnected) {
      return;
    }

    var (ipEndPoint, nonce) = _endpoint;
    var tcpClient = new TcpClient();

    await tcpClient.ConnectAsync(ipEndPoint, ct).ConfigureAwait(false);
    var networkStream = tcpClient.GetStream();

    await networkStream.WriteAsync(nonce, ct).ConfigureAwait(false);
    await networkStream.FlushAsync(ct).ConfigureAwait(false);
    await tcpClient.DiscardAvailableDataAsync(ct).ConfigureAwait(false);

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

    Console.WriteLine($"DEBUG: Written {buffer.Length} bytes asynchronously.");
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

    while (!ct.IsCancellationRequested) {
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
  public async Task CloseAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(TcpClientConnection));

    if (!IsConnected) {
      throw new AssuanClientException("TCP client is not connected.");
    }

    await _networkStream.WriteAsync(Commands.Bye, ct).ConfigureAwait(false);
    await _networkStream.FlushAsync(ct).ConfigureAwait(false);
    await _tcpClient.DiscardAvailableDataAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    if (IsConnected) {
      Close();
    }

    _networkStream?.Dispose();
    _tcpClient?.Dispose();
    _networkStream = null;
    _tcpClient = null;
    _disposed = true;
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    if (IsConnected) {
      await CloseAsync().ConfigureAwait(false);
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
        await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
        return;
      }

      resource.Dispose();
    }
  }
}
