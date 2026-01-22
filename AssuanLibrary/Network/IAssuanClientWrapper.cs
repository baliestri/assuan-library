// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Network;

/// <summary>
///   Represents a wrapper for an Assuan client.
/// </summary>
public interface IAssuanClientWrapper : IAsyncDisposable, IDisposable {
  /// <summary>
  ///   Indicates whether the client is currently connected.
  /// </summary>
  bool IsConnected { get; }

  /// <summary>
  ///   Connects to the remote connection.
  /// </summary>
  void Connect();

  /// <summary>
  ///   Writes data to the remote connection.
  /// </summary>
  /// <param name="buffer">The data to write.</param>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  void Write(byte[] buffer);

  /// <summary>
  ///   Reads data from the remote connection.
  /// </summary>
  /// <returns>An enumerable of read-only memory segments containing the data read.</returns>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  byte[] Read();

  /// <summary>
  ///   Disconnects from the remote connection gracefully.
  /// </summary>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  void Disconnect();

  /// <summary>
  ///   Connects to the remote connection.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  Task ConnectAsync(CancellationToken ct = default);

  /// <summary>
  ///   Writes data to the remote connection.
  /// </summary>
  /// <param name="buffer">The data to write.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  Task WriteAsync(byte[] buffer, CancellationToken ct = default);

  /// <summary>
  ///   Reads data from the remote connection.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous read operation, containing the data read.</returns>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  ValueTask<byte[]> ReadAsync(CancellationToken ct = default);

  /// <summary>
  ///   Disconnects from the remote connection gracefully.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <exception cref="AssuanClientException">Thrown when the remote connection is not connected.</exception>
  Task DisconnectAsync(CancellationToken ct = default);
}
