// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Client;

[ExcludeFromCodeCoverage]
internal static class Commands {
  public static readonly byte[] Data = "D "u8.ToArray();
  public static readonly byte[] End = "END\n"u8.ToArray();
  public static readonly byte[] Cancel = "CANCEL\n"u8.ToArray();
}
