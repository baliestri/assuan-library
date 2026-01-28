// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.Versioning;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Unix.Transport.Endpoints;

/// <summary>
///   Defines a Unix Domain Socket communication endpoint for Assuan protocol.
/// </summary>
/// <param name="Path">The file system path of the Unix Domain Socket.</param>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public readonly record struct UnixDomainSocketEndpoint(string Path) : IAssuanEndpoint {
  /// <summary>
  ///   Deletes the Unix Domain Socket file if it exists.
  /// </summary>
  public void DeleteIfExists() {
    if (File.Exists(Path)) {
      File.Delete(Path);
    }
  }
}
