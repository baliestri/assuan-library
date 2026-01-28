// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.Versioning;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Windows.Transport.Endpoints;

/// <summary>
///   Defines a Named Pipe communication endpoint for Assuan protocol.
/// </summary>
/// <param name="Name">The name of the Named Pipe.</param>
[SupportedOSPlatform("windows")]
public readonly record struct NamedPipeEndpoint(string Name) : IAssuanEndpoint;
