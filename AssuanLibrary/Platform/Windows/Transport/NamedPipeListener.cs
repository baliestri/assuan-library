// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.Versioning;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Windows.Transport;

[SupportedOSPlatform("windows")]
internal sealed class NamedPipeListener(NamedPipeEndpoint endpoint, AssuanListenerOptions options) : IAssuanListener {
  private bool _disposed;
  private NamedPipeServerStream? _serverStream;

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_serverStream))]
  public bool IsListening { get; private set; }

  /// <inheritdoc />
  public IAssuanEndpoint Endpoint { get; } = endpoint;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeListener));

    if (IsListening) {
      return new NamedPipeConnection(_serverStream, endpoint, AssuanConnectionOptions.Default);
    }

    _serverStream = new NamedPipeServerStream(endpoint.Name, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.None);
    _serverStream.WaitForConnection();

    IsListening = true;

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    var connection = new NamedPipeConnection(_serverStream, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);

    return connection;
  }

  /// <inheritdoc />
  public async ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(NamedPipeListener));

    if (IsListening) {
      return new NamedPipeConnection(_serverStream, endpoint, AssuanConnectionOptions.Default);
    }

    _serverStream = new NamedPipeServerStream(endpoint.Name, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
    await _serverStream.WaitForConnectionAsync(ct).ConfigureAwait(false);

    IsListening = true;

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    var connection = new NamedPipeConnection(_serverStream, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);

    return connection;
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _serverStream?.Dispose();
    _serverStream = null;
    _disposed = true;
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    if (_serverStream is not null) {
      await _serverStream.DisposeAsync().ConfigureAwait(false);
    }

    _serverStream = null;
    _disposed = true;
  }
}
