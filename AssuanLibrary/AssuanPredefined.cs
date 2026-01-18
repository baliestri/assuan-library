// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary;

internal static class AssuanPredefined {
  public const int INITIAL_COLLECTION_SIZE = 4;
  public const int MAX_BUFFER_SIZE = 1002; // 1000 bytes + [CR]LF

  public static class Characters {
    public const byte SPACE = 0x20;
    public const byte LF = 0x0A;
    public const byte CR = 0x0D;
  }
}
