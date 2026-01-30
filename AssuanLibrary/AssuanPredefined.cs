// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary;

internal static class AssuanPredefined {
  public const int INITIAL_COLLECTION_SIZE = 4;

  public static class Characters {
    public const byte SPACE = 0x20;
    public const byte LINE_FEED = 0x0A;
    public const byte CARRIAGE_RETURN = 0x0D;
    public const byte TABULATION = 0x09;
    public const byte OPEN_PARENTHESIS = 0x28;
    public const byte CLOSE_PARENTHESIS = 0x29;
    public const byte COLON = 0x3A;
    public const byte HYPHEN = 0x2D;
    public const byte UNDERSCORE = 0x5F;
    public const byte PERIOD = 0x2E;
    public const byte SLASH = 0x2F;
    public const byte QUESTION_MARK = 0x3F;
    public const byte EQUALS = 0x3D;
    public const byte ASTERISK = 0x2A;
    public const byte PERCENT = 0x25;

    public const byte LOWER_A = 0x61;
    public const byte LOWER_Z = 0x7A;
    public const byte UPPER_A = 0x41;
    public const byte UPPER_Z = 0x5A;

    public const byte DIGIT_ZERO = 0x30;
    public const byte DIGIT_NINE = 0x39;
  }
}
