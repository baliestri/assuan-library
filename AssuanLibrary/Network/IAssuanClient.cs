// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;

namespace AssuanLibrary.Network;

/// <summary>
///   Represents a client for communicating with an Assuan server over TCP.
/// </summary>
public interface IAssuanClient : IAsyncDisposable, IDisposable {
  /// <summary>
  ///   Gets a value indicating whether the TCP client is currently connected.
  /// </summary>
  bool IsConnected { get; }

  /// <summary>
  ///   Connects to the server using the specified <see cref="AssuanClientOptions" />.
  /// </summary>
  void Connect();

  /// <summary>
  ///   Connects to the server using the specified IP address and port/nonce information.
  /// </summary>
  /// <param name="ipAddress">The IP address to connect to.</param>
  /// <param name="portAndNonce">The port and nonce information to use when connecting.</param>
  void Connect(IPAddress ipAddress, PortAndNonce portAndNonce);

  /// <summary>
  ///   Disconnects from the server.
  /// </summary>
  void Disconnect();

  /// <summary>
  ///   Connects to the server asynchronously using the specified <see cref="AssuanClientOptions" />.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous connect operation.</returns>
  Task ConnectAsync(CancellationToken ct = default);

  /// <summary>
  ///   Connects to the server asynchronously using the specified IP address and port/nonce information.
  /// </summary>
  /// <param name="ipAddress">The IP address to connect to.</param>
  /// <param name="portAndNonce">The port and nonce information to use when connecting.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous connect operation.</returns>
  Task ConnectAsync(IPAddress ipAddress, PortAndNonce portAndNonce, CancellationToken ct = default);

  /// <summary>
  ///   Disconnects from the server asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous disconnect operation.</returns>
  Task DisconnectAsync(CancellationToken ct = default);

  /// <summary>
  ///   Invokes the specified command.
  /// </summary>
  /// <param name="command">The command to invoke.</param>
  /// <returns>The response collection from the invoked command.</returns>
  AssuanResponseCollection Invoke(AssuanCommand command);

  /// <summary>
  ///   Invokes the specified command asynchronously.
  /// </summary>
  /// <param name="command">The command to invoke.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous invoke operation, containing the response collection from the invoked command.</returns>
  Task<AssuanResponseCollection> InvokeAsync(AssuanCommand command, CancellationToken ct = default);
}
