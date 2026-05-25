// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Transport;

/// <summary>
///   Factory interface for creating Assuan listeners.
/// </summary>
public interface IAssuanListenerFactory {
  /// <summary>
  ///   Creates a new Assuan listener for the specified endpoint.
  /// </summary>
  /// <param name="endpoint">The endpoint to create the listener for.</param>
  /// <returns>A new instance of <see cref="IAssuanListener" />.</returns>
  IAssuanListener CreateListener(IAssuanEndpoint endpoint);
}
