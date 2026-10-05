// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

internal static class ProtocolFields {
  internal static void ValidateLine(ReadOnlySpan<byte> value) {
    if (value.IndexOfAny((byte)0, (byte)'\r', (byte)'\n') >= 0) {
      throw new ProtocolException(ProtocolErrorKind.InvalidLine);
    }
  }

  internal static void ValidateToken(ReadOnlySpan<byte> token) {
    if (token.IsEmpty ||
        token[0] == (byte)'#') {
      throw new ProtocolException(ProtocolErrorKind.InvalidToken);
    }

    foreach (var value in token) {
      if (value is < 33 or > 126 ||
          value == (byte)'%') {
        throw new ProtocolException(ProtocolErrorKind.InvalidToken);
      }
    }
  }

  internal static void ValidateKeyword(ReadOnlySpan<byte> keyword) {
    ValidateToken(keyword);
    var first = keyword[0];
    var isLetter = first is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';
    if (first != (byte)'_' &&
        !isLetter) {
      throw new ProtocolException(ProtocolErrorKind.InvalidToken);
    }
  }

  internal static void Split(ReadOnlySpan<byte> bytes, out ReadOnlySpan<byte> first, out ReadOnlySpan<byte> rest) {
    var separator = bytes.IndexOf((byte)' ');
    first = separator < 0 ? bytes : bytes[..separator];
    rest = separator < 0 ? ReadOnlySpan<byte>.Empty : bytes[(separator + 1)..];
  }
}
