// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

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
  void Connect();

  /// <summary>
  ///   Disconnects from the server.
  /// </summary>
  void Disconnect();

  /// <summary>
  ///   Connects to the server asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous connect operation.</returns>
  Task ConnectAsync(CancellationToken ct = default);

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
  ///   Invokes the specified command with an inquire handler.
  /// </summary>
  /// <param name="command">The command to invoke.</param>
  /// <param name="inquireHandler">The inquire handler to process inquire requests.</param>
  /// <returns>The response collection from the invoked command.</returns>
  AssuanResponseCollection Invoke(AssuanCommand command, InquireHandler inquireHandler);

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
}
