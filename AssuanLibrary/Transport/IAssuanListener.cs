// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Transport;

/// <summary>
///   Represents a listener that accepts incoming Assuan connections.
/// </summary>
public interface IAssuanListener : IAsyncDisposable, IDisposable {
  /// <summary>
  ///   Indicates whether the listener is currently active and listening for incoming connections.
  /// </summary>
  bool IsListening { get; }

  /// <summary>
  ///   Gets the endpoint this listener is bound to.
  /// </summary>
  IAssuanEndpoint Endpoint { get; }

  /// <summary>
  ///   Accepts an incoming connection.
  /// </summary>
  /// <returns>An <see cref="IAssuanConnection" /> representing the accepted connection.</returns>
  IAssuanConnection Accept();

  /// <summary>
  ///   Accepts an incoming connection asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous accept operation, containing the accepted connection.</returns>
  ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default);
}
