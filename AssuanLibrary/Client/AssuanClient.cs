// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Platform.Unix.Transport.Endpoints;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Client;

/// <inheritdoc />
public sealed class AssuanClient(IAssuanEndpointResolver endpointResolver, IAssuanConnectionFactory connectionFactory, AssuanClientOptions options)
  : IAssuanClient {
  private IAssuanConnection? _connection;
  private bool _disposed;

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClient" /> class with the specified endpoint resolver.
  /// </summary>
  /// <param name="endpointResolver">The endpoint resolver to resolve endpoints.</param>
  public AssuanClient(IAssuanEndpointResolver endpointResolver)
    : this(endpointResolver, CreateDefaultFactory(AssuanClientOptions.Default), AssuanClientOptions.Default) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClient" /> class with the specified endpoint resolver and options.
  /// </summary>
  /// <param name="endpointResolver">The endpoint resolver to resolve endpoints.</param>
  /// <param name="options">The configuration options for the client.</param>
  public AssuanClient(IAssuanEndpointResolver endpointResolver, AssuanClientOptions options)
    : this(endpointResolver, CreateDefaultFactory(options), options) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClient" /> class with the specified connection factory.
  /// </summary>
  /// <param name="connectionFactory">The connection factory to create connections.</param>
  public AssuanClient(IAssuanConnectionFactory connectionFactory)
    : this(CreateDefaultResolver(), connectionFactory, AssuanClientOptions.Default) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClient" /> class with the specified connection factory and options.
  /// </summary>
  /// <param name="connectionFactory">The connection factory to create connections.</param>
  /// <param name="options">The configuration options for the client.</param>
  public AssuanClient(IAssuanConnectionFactory connectionFactory, AssuanClientOptions options)
    : this(CreateDefaultResolver(), connectionFactory, options) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClient" /> class with the specified options.
  /// </summary>
  /// <param name="options">The configuration options for the client.</param>
  public AssuanClient(AssuanClientOptions options)
    : this(CreateDefaultResolver(), CreateDefaultFactory(options), options) { }

  /// <summary>
  ///   Initializes a new instance of the <see cref="AssuanClient" /> class with default settings.
  /// </summary>
  public AssuanClient()
    : this(CreateDefaultResolver(), CreateDefaultFactory(AssuanClientOptions.Default), AssuanClientOptions.Default) { }

  /// <inheritdoc />
  [MemberNotNullWhen(true, nameof(_connection))]
  public bool IsConnected => _connection is { IsConnected: true };

  /// <inheritdoc />
  public void Connect(IAssuanEndpoint endpoint, IReadOnlyDictionary<string, object> metadata) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      _connection = connectionFactory.CreateConnection(endpoint);
      _connection.Open();

      options.OnSessionAuthenticatingAsync?.Invoke(_connection, metadata).GetAwaiter().GetResult();

      var banner = _connection.Read();
      var sessionMetadata = new Dictionary<string, object> { ["banner"] = banner };
      options.OnSessionStartedAsync?.Invoke(_connection, sessionMetadata).GetAwaiter().GetResult();
    }
    catch (SocketException ex) {
      Dispose();
      throw new AssuanClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public void Connect(AssuanEndpointKind endpointKind) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      var (resolvedEndpoint, metadata) = endpointResolver.Resolve(endpointKind);
      _connection = connectionFactory.CreateConnection(resolvedEndpoint);
      _connection.Open();

      options.OnSessionAuthenticatingAsync?.Invoke(_connection, metadata).GetAwaiter().GetResult();

      var banner = _connection.Read();
      var sessionMetadata = new Dictionary<string, object> { ["banner"] = banner };
      options.OnSessionStartedAsync?.Invoke(_connection, sessionMetadata).GetAwaiter().GetResult();
    }
    catch (SocketException ex) {
      Dispose();
      throw new AssuanClientException("Failed to connect to the Assuan server.", ex);
    }
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
    _connection.Write(writtenBuffer);

    var readBuffer = _connection.Read();
    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public AssuanResponseCollection Invoke(AssuanCommand command, InquireHandler inquireHandler) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToBytes();
    _connection.Write(writtenBuffer);

    var readBuffer = _connection.Read(inquireHandler);
    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public void Disconnect() {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return;
    }

    options.OnSessionEndingAsync?.Invoke(_connection, CancellationToken.None).GetAwaiter().GetResult();

    _connection.Close();
  }

  /// <inheritdoc />
  public async Task ConnectAsync(IAssuanEndpoint endpoint, IReadOnlyDictionary<string, object> metadata, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      _connection = connectionFactory.CreateConnection(endpoint);
      await _connection.OpenAsync(ct).ConfigureAwait(false);

      if (options.OnSessionAuthenticatingAsync is not null) {
        await options.OnSessionAuthenticatingAsync(_connection, metadata, ct).ConfigureAwait(false);
      }

      var bannerTask = _connection.ReadAsync(ct).AsTask();
      if (options.OnSessionStartedAsync is not null) {
        var banner = await bannerTask.ConfigureAwait(false);
        var sessionMetadata = new Dictionary<string, object> { ["banner"] = banner.ToArray() };
        await options.OnSessionStartedAsync(_connection, sessionMetadata, ct).ConfigureAwait(false);
        return;
      }

      _ = await bannerTask.ConfigureAwait(false);
    }
    catch (Exception ex) {
      await DisposeAsync().ConfigureAwait(false);
      throw new AssuanClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public async Task ConnectAsync(AssuanEndpointKind endpointKind, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (IsConnected) {
      return;
    }

    try {
      var (resolvedEndpoint, metadata) = endpointResolver.Resolve(endpointKind);
      _connection = connectionFactory.CreateConnection(resolvedEndpoint);
      await _connection.OpenAsync(ct).ConfigureAwait(false);

      if (options.OnSessionAuthenticatingAsync is not null) {
        await options.OnSessionAuthenticatingAsync(_connection, metadata, ct).ConfigureAwait(false);
      }

      var bannerTask = _connection.ReadAsync(ct).AsTask();
      if (options.OnSessionStartedAsync is not null) {
        var banner = await bannerTask.ConfigureAwait(false);
        var sessionMetadata = new Dictionary<string, object> { ["banner"] = banner.ToArray() };
        await options.OnSessionStartedAsync(_connection, sessionMetadata, ct).ConfigureAwait(false);
        return;
      }

      _ = await bannerTask.ConfigureAwait(false);
    }
    catch (Exception ex) {
      await DisposeAsync().ConfigureAwait(false);
      throw new AssuanClientException("Failed to connect to the Assuan server.", ex);
    }
  }

  /// <inheritdoc />
  public async ValueTask<AssuanResponseCollection> InvokeAsync(AssuanCommand command, CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToReadOnlyMemory();
    await _connection.WriteAsync(writtenBuffer, ct).ConfigureAwait(false);

    var readBuffer = await _connection.ReadAsync(ct).ConfigureAwait(false);
    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public async ValueTask<AssuanResponseCollection> InvokeAsync(AssuanCommand command, AsyncInquireHandler inquireHandler,
  CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return options.ThrowIfNotConnected
        ? throw new AssuanClientException("The client is not connected to the server.")
        : new AssuanResponseCollection();
    }

    var writtenBuffer = command.ToReadOnlyMemory();
    await _connection.WriteAsync(writtenBuffer, ct).ConfigureAwait(false);

    var readBuffer = await _connection.ReadAsync(inquireHandler, ct).ConfigureAwait(false);
    return new AssuanResponseCollection(readBuffer);
  }

  /// <inheritdoc />
  public async Task DisconnectAsync(CancellationToken ct = default) {
    ObjectDisposedException.ThrowIf(_disposed, nameof(AssuanClient));

    if (!IsConnected) {
      return;
    }

    if (options.OnSessionEndingAsync is not null) {
      await options.OnSessionEndingAsync(_connection, ct).ConfigureAwait(false);
    }

    await _connection.CloseAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _connection?.Dispose();
    _connection = null;
    _disposed = true;
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (_disposed) {
      return;
    }

    if (_connection is not null) {
      await _connection.DisposeAsync().ConfigureAwait(false);
    }

    _connection = null;
    _disposed = true;
  }

  private static IAssuanConnectionFactory CreateDefaultFactory(AssuanClientOptions options)
    => new DefaultConnectionFactory(options);

  private static IAssuanEndpointResolver CreateDefaultResolver() {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      return new TcpClientEndpointResolver(); // Since it was not provided, we default to TCP on Windows
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      return new UnixDomainSocketEndpointResolver();
    }

    throw new PlatformNotSupportedException();
  }
}
