// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.SExpressions;

internal static class SExpressionScanner {
  internal static int Scan(ReadOnlySpan<byte> input, SExpressionLimits limits, int start) {
    var position = start;
    var depth = 0;
    var nodes = 0;

    while (true) {
      if (position == input.Length) {
        throw new SExpressionParseException(SExpressionParseErrorKind.UnexpectedEof, position);
      }

      var current = input[position];
      switch (current) {
        case (byte)'(' when depth >= limits.MaxDepth:
          throw new SExpressionParseException(SExpressionParseErrorKind.DepthLimit, position);
        case (byte)'(':
          CountNode(ref nodes, limits, position);
          ++depth;
          ++position;
          continue;
        case (byte)')' when depth == 0:
          throw new SExpressionParseException(SExpressionParseErrorKind.UnexpectedToken, position);
        case (byte)')': {
          --depth;
          ++position;
          if (depth == 0) {
            return position;
          }

          continue;
        }
        case < (byte)'0' or > (byte)'9':
          throw new SExpressionParseException(SExpressionParseErrorKind.UnexpectedToken, position);
      }

      CountNode(ref nodes, limits, position);
      var lengthStart = position;
      ulong length = 0;
      while (position < input.Length &&
             input[position] is >= (byte)'0' and <= (byte)'9') {
        if (position != lengthStart &&
            input[lengthStart] == (byte)'0') {
          throw new SExpressionParseException(SExpressionParseErrorKind.InvalidLength, position);
        }

        var digit = (uint)(input[position] - (byte)'0');
        if (length > ((ulong.MaxValue - digit) / 10)) {
          throw new SExpressionParseException(SExpressionParseErrorKind.LengthOverflow, position);
        }

        length = (length * 10) + digit;
        ++position;
      }

      if (position == input.Length) {
        throw new SExpressionParseException(SExpressionParseErrorKind.UnexpectedEof, position);
      }

      if (input[position] != (byte)':') {
        throw new SExpressionParseException(SExpressionParseErrorKind.ExpectedColon, position);
      }

      ++position;
      if (length > (ulong)limits.MaxAtomBytes) {
        throw new SExpressionParseException(SExpressionParseErrorKind.AtomLimit, lengthStart);
      }

      if (length > (ulong)(input.Length - position)) {
        throw new SExpressionParseException(SExpressionParseErrorKind.UnexpectedEof, input.Length);
      }

      position += (int)length;
      if (depth == 0) {
        return position;
      }
    }
  }

  private static void CountNode(ref int nodes, SExpressionLimits limits, int position) {
    if (nodes >= limits.MaxNodes) {
      throw new SExpressionParseException(SExpressionParseErrorKind.NodeLimit, position);
    }

    ++nodes;
  }

  // The caller first validates the entire expression. Revisited subtrees need no new
  // resource checks and may have been parsed with custom limits greater than default.
  internal static int SkipValidated(ReadOnlySpan<byte> input, int start) {
    var position = start;
    var depth = 0;
    while (position < input.Length) {
      var current = input[position++];
      switch (current) {
        case (byte)'(':
          ++depth;
          break;
        case (byte)')': {
          if (--depth == 0) {
            return position;
          }

          break;
        }
        default: {
          var length = current - (byte)'0';
          while (input[position] != (byte)':') {
            length = ((length * 10) + input[position++]) - (byte)'0';
          }

          position += 1 + length;
          if (depth == 0) {
            return position;
          }

          break;
        }
      }
    }

    throw new InvalidOperationException("The previously validated expression was modified.");
  }
}
