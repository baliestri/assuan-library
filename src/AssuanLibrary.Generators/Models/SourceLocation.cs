// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using Microsoft.CodeAnalysis;

namespace AssuanLibrary.Generators.Models;

// A source Location preserves the compilation's actual tree for diagnostics;
// models never retain symbols, syntax nodes or Compilation instances.
internal readonly struct SourceLocation {
  private readonly Location _location;

  internal SourceLocation(Location location)
    => _location = location;

  internal Location ToLocation()
    => _location;

  internal int Start => _location.SourceSpan.Start;
}
