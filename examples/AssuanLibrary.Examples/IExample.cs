// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Examples;

internal interface IExample {
  string Name { get; }
  string Description { get; }

  Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token);
}
