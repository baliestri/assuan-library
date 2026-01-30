// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.Versioning;
using AssuanLibrary.Platform.Unix.Polyfills;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Unix.Transport.Endpoints;

/// <summary>
///   Defines a Unix Domain Socket communication endpoint for Assuan protocol.
/// </summary>
/// <param name="EndPoint">The Unix Domain Socket endpoint of the communication.</param>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public readonly record struct UnixDomainSocketEndpoint(UnixDomainSocketEndPoint EndPoint) : IAssuanEndpoint {
  /// <summary>
  ///   Initializes a new instance of the <see cref="UnixDomainSocketEndpoint" /> record struct with the specified path.
  /// </summary>
  /// <param name="path">The file system path of the Unix Domain Socket.</param>
  public UnixDomainSocketEndpoint(string path) : this(new UnixDomainSocketEndPoint(path)) { }

  /// <summary>
  ///   Implicitly converts a <see cref="UnixDomainSocketEndpoint" /> to a <see cref="UnixDomainSocketEndPoint" />.
  /// </summary>
  /// <param name="endpoint">The Unix Domain Socket endpoint to convert.</param>
  /// <returns>The corresponding Unix Domain Socket endpoint.</returns>
  public static implicit operator UnixDomainSocketEndPoint(UnixDomainSocketEndpoint endpoint)
    => endpoint.EndPoint;
}
