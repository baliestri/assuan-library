// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Endpoints;

namespace AssuanLibrary.Platform.Unix.Endpoints;

/// <summary>
///   Defines a Unix Domain Socket communication endpoint for Assuan protocol.
/// </summary>
/// <param name="Path">The file system path of the Unix Domain Socket.</param>
public readonly record struct UnixDomainSocketEndpoint(string Path) : IAssuanEndpoint;
