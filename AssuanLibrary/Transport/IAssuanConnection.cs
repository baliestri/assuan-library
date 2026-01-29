// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Exceptions;

namespace AssuanLibrary.Transport;

/// <summary>
///   Represents a connection to a remote Assuan server.
/// </summary>
public interface IAssuanConnection : IAsyncDisposable, IDisposable {
  /// <summary>
  ///   Indicates whether the client is currently connected.
  /// </summary>
  bool IsConnected { get; }

  /// <summary>
  ///   Opens the remote connection.
  /// </summary>
  void Open();

  /// <summary>
  ///   Writes data to the remote connection.
  /// </summary>
  /// <param name="buffer">The data to write.</param>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  void Write(byte[] buffer);

  /// <summary>
  ///   Reads data from the remote connection.
  /// </summary>
  /// <returns>A byte array containing the data read.</returns>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  byte[] Read();

  /// <summary>
  ///   Reads data from the remote connection with an inquire handler.
  /// </summary>
  /// <param name="inquireHandler">The inquire handler to process inquire requests.</param>
  /// <returns>A byte array containing the data read.</returns>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  byte[] Read(InquireHandler inquireHandler);

  /// <summary>
  ///   Discards any pending input from the remote connection.
  /// </summary>
  void DiscardPendingInput();

  /// <summary>
  ///   Closes the remote connection.
  /// </summary>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  void Close();

  /// <summary>
  ///   Opens the remote connection asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  Task OpenAsync(CancellationToken ct = default);

  /// <summary>
  ///   Writes data to the remote connection.
  /// </summary>
  /// <param name="buffer">The data to write.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default);

  /// <summary>
  ///   Reads data from the remote connection.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous read operation, containing the data read.</returns>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default);

  /// <summary>
  ///   Reads data from the remote connection with an inquire handler.
  /// </summary>
  /// <param name="inquireHandler">The inquire handler to process inquire requests.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous read operation, containing the data read.</returns>
  ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler, CancellationToken ct = default);

  /// <summary>
  ///   Discards any pending input from the remote connection asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous discard operation.</returns>
  ValueTask DiscardPendingInputAsync(CancellationToken ct = default);

  /// <summary>
  ///   Closes the remote connection asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  Task CloseAsync(CancellationToken ct = default);
}
