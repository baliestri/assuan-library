// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests;

/// <summary>Locates the checkout from a test output directory, including RID-specific outputs.</summary>
internal static class RepositoryRoot {
  internal static string Path { get; } = Find(AppContext.BaseDirectory);

  private static string Find(string directory) {
    for (var current = new DirectoryInfo(System.IO.Path.GetFullPath(directory));
         current is not null;
         current = current.Parent)
      if (File.Exists(System.IO.Path.Combine(current.FullName, "AssuanLibrary.slnx"))) {
        return current.FullName;
      }

    throw new DirectoryNotFoundException("AssuanLibrary checkout was not found.");
  }
}
