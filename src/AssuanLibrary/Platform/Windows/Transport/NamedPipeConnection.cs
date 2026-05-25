// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.Versioning;
using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Extensions;
using AssuanLibrary.Platform.Windows.Extensions;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport.IO;
using AssuanLibrary.Polyfills;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Windows.Transport;

[SupportedOSPlatform("windows")]
internal sealed class NamedPipeConnection : IAssuanConnection {
  private readonly NamedPipeEndpoint _endpoint;
  private readonly AssuanConnectionOptions _options;
  private readonly StabilizationOptions _stabilizationOptions;
  private bool _disposed;

  private PipeStream? _pipeStream;

  public NamedPipeConnection(NamedPipeEndpoint endpoint, AssuanConnectionOptions options, StabilizationOptions? stabilizationOptions = null) {
    _endpoint = endpoint;
    _options = options;
    _stabilizationOptions = stabilizationOptions ?? StabilizationOptions.Default;
    _options.ConfigureStabilization?.Invoke(_stabilizationOptions);
  }

  public NamedPipeConnection(NamedPipeServerStream pipeServer, NamedPipeEndpoint endpoint, AssuanConnectionOptions options,
  StabilizationOptions? stabilizationOptions = null) {
    _endpoint = endpoint;
    _options = options;
    _stabilizationOptions = stabilizationOptions ?? StabilizationOptions.Default;
    _options.ConfigureStabilization?.Invoke(_stabilizationOptions);
    _pipeStream = pipeServer;
  }

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_pipeStream))]
  public bool IsConnected => _pipeStream is { IsConnected: true };

  /// <inheritdoc />
  public void Open() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    var pipeClient = new NamedPipeClientStream(_endpoint.Server, _endpoint.Name, PipeDirection.InOut, PipeOptions.None);

    pipeClient.Connect(_options.TimeoutInMilliseconds);
    pipeClient.ReadMode = PipeTransmissionMode.Message;

    _pipeStream = pipeClient;
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    _pipeStream.Write(buffer);
    _pipeStream.Flush();
  }

  /// <inheritdoc />
  public byte[] Read() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    using var reader = new StabilizedNamedPipeReader(_pipeStream, _options.TimeoutInMilliseconds, _stabilizationOptions);
    return reader.Read();
  }

  /// <inheritdoc />
  public byte[] Read(InquireHandler inquireHandler) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();

    while (true) {
      var b = _pipeStream.ReadByte();
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
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    using var ms = new MemoryStream();
    Span<byte> buffer = stackalloc byte[256];

    while (true) {
      var read = _pipeStream.Read(buffer);

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
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    _pipeStream.DiscardAvailableData();
  }

  /// <inheritdoc />
  public void Close() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      return;
    }

    _pipeStream.Close();
  }

  /// <inheritdoc />
  public async Task OpenAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    var pipeClient = new NamedPipeClientStream(".", _endpoint.Name, PipeDirection.InOut, PipeOptions.Asynchronous);

    await pipeClient.ConnectAsync(_options.TimeoutInMilliseconds, ct).ConfigureAwait(false);
    pipeClient.ReadMode = PipeTransmissionMode.Message;

    _pipeStream = pipeClient;
  }

  /// <inheritdoc />
  public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    await _pipeStream.WriteAsync(buffer, ct).ConfigureAwait(false);
    await _pipeStream.FlushAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    using var reader = new StabilizedNamedPipeReader(_pipeStream, _options.TimeoutInMilliseconds, _stabilizationOptions);
    return await reader.ReadAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();

    while (!ct.IsCancellationRequested) {
      var b = _pipeStream.ReadByte();
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
  public async ValueTask<ReadOnlyMemory<byte>> ReadAvailableAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    using var ms = new MemoryStream();
    using var memoryOwner = MemoryPool<byte>.Shared.Rent(256);
    var memory = memoryOwner.Memory;

    while (true) {
      ct.ThrowIfCancellationRequested();

      var read = await _pipeStream.ReadAsync(memory, ct).ConfigureAwait(false);

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
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      throw new AssuanClientException("The named pipe connection is not open.");
    }

    await _pipeStream.DiscardAvailableDataAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task CloseAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeConnection));

    if (!IsConnected) {
      return;
    }

    await Task.Run(() => _pipeStream.Close(), ct);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    try {
      _pipeStream?.Dispose();
    }
    finally {
      _pipeStream = null;
      _disposed = true;
    }
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    try {
      if (_pipeStream is not null) {
        await _pipeStream.DisposeAsync().ConfigureAwait(false);
      }
    }
    finally {
      _pipeStream = null;
      _disposed = true;
    }
  }
}
