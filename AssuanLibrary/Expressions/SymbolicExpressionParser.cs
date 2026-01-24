// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Exceptions;

namespace AssuanLibrary.Expressions;

/// <summary>
///   Parses symbolic expressions from Assuan responses.
/// </summary>
public static class SymbolicExpressionParser {
  /// <summary>
  ///   Parses a symbolic expression from an <see cref="AssuanResponse" />.
  /// </summary>
  /// <param name="assuanResponse">The <see cref="AssuanResponse" /> to parse.</param>
  /// <param name="bytesConsumed">The number of bytes consumed during parsing.</param>
  /// <returns>The parsed symbolic expression.</returns>
  /// <exception cref="AtomLengthOutOfRangeException">Thrown when the declared length of an atom exceeds its actual length.</exception>
  /// <exception cref="IncompleteSymbolicExpressionException">Thrown when the symbolic expression is incomplete.</exception>
  /// <exception cref="InvalidAssuanResponseTypeException">Thrown when the response type is not <see cref="AssuanResponseType.Data" />.</exception>
  /// <exception cref="InvalidBinaryLengthException">Thrown when a binary length is invalid.</exception>
  /// <exception cref="InvalidSymbolicExpressionSyntaxException">Thrown when the symbolic expression has invalid syntax.</exception>
  public static SymbolicExpression Parse(AssuanResponse? assuanResponse, out int bytesConsumed) {
    bytesConsumed = 0;

    if (assuanResponse is null) {
      throw new ArgumentNullException(nameof(assuanResponse), "The Assuan response cannot be null.");
    }

    if (assuanResponse.Type is not AssuanResponseType.Data) {
      throw new InvalidAssuanResponseTypeException(assuanResponse.Type);
    }

    var depth = 0;
    var symbolicExpression = ParseExpression(assuanResponse.DecodedBuffer, ref bytesConsumed, ref depth);

    return depth is not 0
      ? throw new IncompleteSymbolicExpressionException($"Unclosed symbolic expression collection (expected ')' for depth {depth}).")
      : symbolicExpression;
  }

  /// <summary>
  ///   Tries to parse a symbolic expression from an <see cref="AssuanResponse" />.
  /// </summary>
  /// <param name="assuanResponse">The <see cref="AssuanResponse" /> to parse.</param>
  /// <param name="symbolicExpression">The parsed symbolic expression, if successful.</param>
  /// <param name="bytesConsumed">The number of bytes consumed during parsing.</param>
  /// <returns><see langword="true" /> if the parsing was successful; otherwise, <see langword="false" />.</returns>
  public static bool TryParse(AssuanResponse? assuanResponse, [NotNullWhen(true)] out SymbolicExpression? symbolicExpression, out int bytesConsumed) {
    symbolicExpression = null;
    bytesConsumed = 0;

    if (assuanResponse?.Type is not AssuanResponseType.Data) {
      return false;
    }

    try {
      var depth = 0;
      var expression = ParseExpression(assuanResponse.DecodedBuffer, ref bytesConsumed, ref depth);

      if (depth is not 0) {
        return false;
      }

      symbolicExpression = expression;
      return true;
    }
    catch {
      return false;
    }
  }

  private static SymbolicExpression ParseExpression(ReadOnlySpan<byte> input, ref int position, ref int depth) {
    SkipWhitespace(input, ref position);

    var currentByte = input[position];

    if (position >= input.Length) {
      throw new IncompleteSymbolicExpressionException("Unexpected end of input while parsing symbolic expression.");
    }

    return currentByte == Characters.OPEN_PARENTHESIS
      ? ParseCollection(input, ref position, ref depth)
      : ParseAtom(input, ref position);
  }

  private static SymbolicExpression ParseAtom(ReadOnlySpan<byte> input, ref int position) {
    var length = ReadLength(input, ref position);

    if (position >= input.Length ||
        input[position] != Characters.COLON) {
      throw new InvalidSymbolicExpressionSyntaxException(position, "Expected ':' after atom length");
    }

    position++;

    if ((position + length) > input.Length) {
      throw new AtomLengthOutOfRangeException(length, input.Length - position, position);
    }

    var dataSlice = input.Slice(position, length);
    position += length;

    return new SymbolicExpressionAtom(dataSlice);
  }

  private static SymbolicExpression ParseCollection(ReadOnlySpan<byte> input, ref int position, ref int depth) {
    if (position >= input.Length) {
      throw new IncompleteSymbolicExpressionException("Unexpected end of input while parsing symbolic expression collection.");
    }

    if (input[position] != Characters.OPEN_PARENTHESIS) {
      throw new InvalidSymbolicExpressionSyntaxException(position, "Expected '(' at the beginning of symbolic expression collection");
    }

    position++;
    depth++;

    var children = new List<SymbolicExpression>();

    while (position < input.Length) {
      SkipWhitespace(input, ref position);

      if (position >= input.Length) {
        break;
      }

      var currentByte = input[position];

      if (currentByte == Characters.CLOSE_PARENTHESIS) {
        position++;
        depth--;
        if (depth == 0) {
          return new SymbolicExpressionCollection(children);
        }

        continue;
      }

      var atomOrCollection = ParseExpression(input, ref position, ref depth);
      children.Add(atomOrCollection);
    }

    return new SymbolicExpressionCollection(children);
  }

  private static void SkipWhitespace(ReadOnlySpan<byte> input, ref int position) {
    while (position < input.Length &&
           IsWhitespace(input[position])) {
      position++;
    }
  }

  private static bool IsWhitespace(byte b)
    => b is Characters.SPACE or Characters.TABULATION or Characters.LINE_FEED or Characters.CARRIAGE_RETURN;

  private static int ReadLength(ReadOnlySpan<byte> input, ref int position) {
    var length = 0;
    while (position < input.Length) {
      var currentByte = input[position];

      if (currentByte is < Characters.DIGIT_ZERO or > Characters.DIGIT_NINE) {
        break;
      }

      length = (length * 10) + (currentByte - Characters.DIGIT_ZERO);
      position++;
    }

    return length;
  }
}
