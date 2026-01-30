// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Client.Abstractions;

/// <summary>
///   A client for communicating with a server through the Assuan protocol.
/// </summary>
public interface IAssuanClient : IAsyncDisposable, IDisposable {
  /// <summary>
  ///   Gets a value indicating whether the client is currently connected.
  /// </summary>
  bool IsConnected { get; }

  /// <summary>
  ///   Connects to the server.
  /// </summary>
  /// <param name="endpoint">The endpoint to connect to.</param>
  /// <param name="metadata">The metadata to send during the connection.</param>
  void Connect(IAssuanEndpoint endpoint, IReadOnlyDictionary<string, object> metadata);

  /// <summary>
  ///   Connects to the server.
  /// </summary>
  /// <param name="endpointKind">The kind of endpoint to resolve and connect to.</param>
  void Connect(AssuanEndpointKind endpointKind);

  /// <summary>
  ///   Invokes the specified command.
  /// </summary>
  /// <param name="command">The command to invoke.</param>
  /// <returns>The response collection from the invoked command.</returns>
  AssuanResponseCollection Invoke(AssuanCommand command);

  /// <summary>
  ///   Invokes the specified command with an inquire handler.
  /// </summary>
  /// <param name="command">The command to invoke.</param>
  /// <param name="inquireHandler">The inquire handler to process inquire requests.</param>
  /// <returns>The response collection from the invoked command.</returns>
  AssuanResponseCollection Invoke(AssuanCommand command, InquireHandler inquireHandler);

  /// <summary>
  ///   Disconnects from the server.
  /// </summary>
  void Disconnect();

  /// <summary>
  ///   Connects to the server asynchronously.
  /// </summary>
  /// <param name="endpoint">The endpoint to connect to.</param>
  /// <param name="metadata">The metadata to send during the connection.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous connect operation.</returns>
  Task ConnectAsync(IAssuanEndpoint endpoint, IReadOnlyDictionary<string, object> metadata, CancellationToken ct = default);

  /// <summary>
  ///   Connects to the server asynchronously.
  /// </summary>
  /// <param name="endpointKind">The kind of endpoint to resolve and connect to.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous connect operation.</returns>
  Task ConnectAsync(AssuanEndpointKind endpointKind, CancellationToken ct = default);

  /// <summary>
  ///   Invokes the specified command asynchronously.
  /// </summary>
  /// <param name="command">The command to invoke.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous invoke operation, containing the response collection from the invoked command.</returns>
  ValueTask<AssuanResponseCollection> InvokeAsync(AssuanCommand command, CancellationToken ct = default);

  /// <summary>
  ///   Invokes the specified command with an inquire handler asynchronously.
  /// </summary>
  /// <param name="command">The command to invoke.</param>
  /// <param name="inquireHandler">The inquire handler to process inquire requests.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous invoke operation, containing the response collection from the invoked command.</returns>
  ValueTask<AssuanResponseCollection> InvokeAsync(AssuanCommand command, AsyncInquireHandler inquireHandler, CancellationToken ct = default);

  /// <summary>
  ///   Disconnects from the server asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous disconnect operation.</returns>
  Task DisconnectAsync(CancellationToken ct = default);
}
