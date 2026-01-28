// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Transport;

/// <summary>
///   Factory interface for creating Assuan connections.
/// </summary>
public interface IAssuanConnectionFactory {
  /// <summary>
  ///   Creates a new Assuan connection for the specified endpoint.
  /// </summary>
  /// <param name="endpoint">The endpoint to create the connection for.</param>
  /// <returns>A new instance of <see cref="IAssuanConnection" />.</returns>
  IAssuanConnection CreateConnection(IAssuanEndpoint endpoint);
}
