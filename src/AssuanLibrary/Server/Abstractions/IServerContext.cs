// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   Represents the context of an Assuan server session.
/// </summary>
public interface IServerContext {
  /// <summary>
  ///   Gets the current session associated with this context.
  /// </summary>
  IServerSession Session { get; }

  /// <summary>
  ///   Sends an Assuan response collection back to the client.
  /// </summary>
  /// <param name="responseCollection">The response collection to send.</param>
  void SendResponse(AssuanResponseCollection responseCollection);

  /// <summary>
  ///   Sends an Assuan response back to the client.
  /// </summary>
  /// <param name="response">The response to send.</param>
  void SendResponse(AssuanResponse response);

  /// <summary>
  ///   Inquires data from the client.
  /// </summary>
  /// <param name="keyword">The inquire keyword.</param>
  /// <param name="parameters">The inquire parameters.</param>
  /// <returns>The inquire response data.</returns>
  byte[] Inquire(string keyword, IReadOnlyCollection<string> parameters);

  /// <summary>
  ///   Sends an Assuan response collection back to the client asynchronously.
  /// </summary>
  /// <param name="responseCollection">The response collection to send.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous send operation.</returns>
  Task SendResponseAsync(AssuanResponseCollection responseCollection, CancellationToken ct = default);

  /// <summary>
  ///   Sends an Assuan response back to the client asynchronously.
  /// </summary>
  /// <param name="response">The response to send.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous send operation.</returns>
  Task SendResponseAsync(AssuanResponse response, CancellationToken ct = default);

  /// <summary>
  ///   Inquires data from the client asynchronously.
  /// </summary>
  /// <param name="keyword">The inquire keyword.</param>
  /// <param name="parameters">The inquire parameters.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous send operation, containing the inquire response data.</returns>
  Task<ReadOnlyMemory<byte>> InquireAsync(string keyword, IReadOnlyCollection<string> parameters, CancellationToken ct = default);
}
