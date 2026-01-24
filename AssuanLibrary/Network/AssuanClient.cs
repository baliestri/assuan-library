// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AssuanLibrary.Network.Platform.Unix;
using AssuanLibrary.Network.Platform.Windows;

namespace AssuanLibrary.Network;

/// <inheritdoc />
public sealed class AssuanClient(AssuanClientOptions options) : IAssuanClient {
  private bool _disposed;
  private IAssuanClientWrapper? _wrapper;

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClient" /> class with default options.
  /// </summary>
  public AssuanClient() : this(AssuanClientOptions.Empty) { }

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
      var (socketDescriptor, timeout) = options;
      _wrapper = true switch {
        true when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => new AssuanTcpClientWrapper(socketDescriptor, timeout),
        true when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => new AssuanSocketClientWrapper(socketDescriptor, timeout),
        true when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => new AssuanSocketClientWrapper(socketDescriptor, timeout),
        var _ => throw new PlatformNotSupportedException("The current platform is not supported by the AssuanClient.")
      };

      _wrapper.Connect();
      _wrapper.Write(Keywords.OptionPinentryModeLoopback);
      _ = _wrapper.Read();
    }
    catch (SocketException ex) {
      Dispose();
      throw new AssuanClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public void Disconnect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return;
    }

    _wrapper.Disconnect();
  }

  /// <inheritdoc />
  public async Task ConnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      var (socketDescriptor, timeout) = options;
      _wrapper = true switch {
        true when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => new AssuanTcpClientWrapper(socketDescriptor, timeout),
        true when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => new AssuanSocketClientWrapper(socketDescriptor, timeout),
        true when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => new AssuanSocketClientWrapper(socketDescriptor, timeout),
        var _ => throw new PlatformNotSupportedException("The current platform is not supported by the AssuanClient.")
      };

      await _wrapper.ConnectAsync(ct);
      await _wrapper.WriteAsync(Keywords.OptionPinentryModeLoopback, ct);
      _ = await _wrapper.ReadAsync(ct);
    }
    catch (SocketException ex) {
      await DisposeAsync();
      throw new AssuanClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public async Task DisconnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return;
    }

    await _wrapper.DisconnectAsync(ct);
  }

  /// <inheritdoc />
  public AssuanResponseCollection Invoke(AssuanCommand command) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToBytes();
    _wrapper.Write(writtenBuffer);

    var readBuffer = _wrapper.Read();
    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public AssuanResponseCollection Invoke(AssuanCommand command, Action<IInquireContext> inquireHandler) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToBytes();
    _wrapper.Write(writtenBuffer);

    var readBuffer = _wrapper.Read(inquireHandler);
    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public async ValueTask<AssuanResponseCollection> InvokeAsync(AssuanCommand command, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToBytes();
    await _wrapper.WriteAsync(writtenBuffer, ct);

    var readBuffer = await _wrapper.ReadAsync(ct);
    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public async ValueTask<AssuanResponseCollection> InvokeAsync(AssuanCommand command, Func<IInquireContext, CancellationToken, Task> inquireHandler,
  CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToBytes();
    await _wrapper.WriteAsync(writtenBuffer, ct);

    var readBuffer = await _wrapper.ReadAsync(inquireHandler, ct);
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
