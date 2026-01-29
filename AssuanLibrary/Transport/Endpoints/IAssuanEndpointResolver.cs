// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Endpoints;

/// <summary>
///   Defines a resolver for Assuan endpoints.
/// </summary>
public interface IAssuanEndpointResolver {
  /// <summary>
  ///   Resolves an endpoint of the specified kind.
  /// </summary>
  /// <param name="kind">The kind of endpoint to resolve.</param>
  /// <returns>The resolution of the endpoint.</returns>
  AssuanEndpointResolution Resolve(AssuanEndpointKind kind);
}
