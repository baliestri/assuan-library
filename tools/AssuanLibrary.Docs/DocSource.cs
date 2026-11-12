// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Docs;

/// <summary>One reviewed source in the versioned documentation manifest.</summary>
public sealed record DocSource(
  int Order,
  string Title,
  string Source,
  string Destination,
  bool IncludeInIndex,
  bool IncludeInFull,
  bool Optional);
