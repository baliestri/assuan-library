// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Endpoints.Abstractions;

/// <summary>
///   Resolves an <see cref="IAssuanEndpoint" /> based on the specified <see cref="AssuanEndpointKind" />.
/// </summary>
public interface IAssuanEndpointResolver {
  /// <summary>
  ///   Resolves an <see cref="IAssuanEndpoint" /> based on the specified <see cref="AssuanEndpointKind" />.
  /// </summary>
  /// <param name="kind">The kind of Assuan endpoint to resolve.</param>
  /// <returns>The resolved <see cref="IAssuanEndpoint" />.</returns>
  IAssuanEndpoint Resolve(AssuanEndpointKind kind);
}
