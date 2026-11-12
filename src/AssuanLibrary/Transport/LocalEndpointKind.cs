// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport;

/// <summary>Kind of explicit local byte-stream endpoint.</summary>
public enum LocalEndpointKind {
  /// <summary>A filesystem Unix-domain socket.</summary>
  UnixSocket,

  /// <summary>A local Windows named pipe.</summary>
  NamedPipe
}
