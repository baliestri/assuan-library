// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace AssuanLibrary.Network;

/// <inheritdoc />
public sealed class AssuanClient(AssuanClientOptions options) : IAssuanClient {
  private bool _disposed;
  private AssuanTcpClientWrapper? _wrapper;

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_wrapper))]
  public bool IsConnected => _wrapper is { IsConnected: true };

  /// <inheritdoc />
  public void Connect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      var (ipAddress, portAndNonce) = options;

      _wrapper = new AssuanTcpClientWrapper();
      _wrapper.Connect(ipAddress, portAndNonce);
    }
    catch (SocketException ex) {
      Dispose();
      throw new AssuanTcpClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public void Connect(IPAddress ipAddress, PortAndNonce portAndNonce) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      _wrapper = new AssuanTcpClientWrapper();
      _wrapper.Connect(ipAddress, portAndNonce);
    }
    catch (SocketException ex) {
      Dispose();
      throw new AssuanTcpClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public async Task ConnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      var (ipAddress, portAndNonce) = options;

      _wrapper = new AssuanTcpClientWrapper();
      await _wrapper.ConnectAsync(ipAddress, portAndNonce, ct);
    }
    catch (SocketException ex) {
      Dispose();
      throw new AssuanTcpClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public async Task ConnectAsync(IPAddress ipAddress, PortAndNonce portAndNonce, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      _wrapper = new AssuanTcpClientWrapper();
      await _wrapper.ConnectAsync(ipAddress, portAndNonce, ct);
    }
    catch (SocketException ex) {
      Dispose();
      throw new AssuanTcpClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public AssuanResponseCollection Invoke(AssuanCommand command) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanTcpClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToBytes();
    _wrapper.Write(writtenBuffer);

    var readBuffer = _wrapper.Read();

    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public async Task<AssuanResponseCollection> InvokeAsync(AssuanCommand command, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanTcpClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToBytes();
    await _wrapper.WriteAsync(writtenBuffer, ct);

    var readBuffer = await _wrapper.ReadAsync(ct);

    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _wrapper?.Dispose();
    _wrapper = null;
    _disposed = true;
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    if (_wrapper is not null) {
      await _wrapper.DisposeAsync();
    }

    _wrapper = null;
    _disposed = true;
  }
}
