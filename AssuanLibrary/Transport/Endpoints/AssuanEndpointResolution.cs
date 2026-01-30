// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Endpoints;

/// <summary>
///   Represents the result of an endpoint resolution.
/// </summary>
/// <param name="Endpoint">The resolved endpoint.</param>
/// <param name="Metadata">Additional metadata associated with the resolution.</param>
public sealed record AssuanEndpointResolution(IAssuanEndpoint Endpoint, IReadOnlyDictionary<string, object> Metadata);
