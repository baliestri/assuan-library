// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary;

internal static class AssuanPredefined {
  public const int INITIAL_COLLECTION_SIZE = 4;
  public const int DEFAULT_TIMEOUT_SECONDS = 30;

  public static class Characters {
    public const byte SPACE = 0x20;
    public const byte LINE_FEED = 0x0A;
    public const byte CARRIAGE_RETURN = 0x0D;
    public const byte TABULATION = 0x09;
    public const byte OPEN_PARENTHESIS = 0x28;
    public const byte CLOSE_PARENTHESIS = 0x29;
    public const byte COLON = 0x3A;

    public const byte DIGIT_ZERO = 0x30;
    public const byte DIGIT_NINE = 0x39;
    public const byte BINARY_LENGTH_CONTINUATION_BIT = 0x80;
    public const byte BINARY_LENGTH_VALUE_MASK = 0x7F;
  }
}
