// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary.Expressions;

public abstract partial class SymbolicExpression {
  /// <summary>
  ///   Pretty prints the symbolic expression with indentation for better readability.
  /// </summary>
  /// <param name="symbolicExpression">The symbolic expression to pretty print.</param>
  /// <param name="indentSize">The number of spaces to use for each indentation level. Default is 2.</param>
  /// <returns>A pretty-printed string representation of the symbolic expression.</returns>
  public static string PrettyPrint(SymbolicExpression symbolicExpression, int indentSize = 2) {
    var stringBuilder = new StringBuilder();

    WriteExpression(symbolicExpression, stringBuilder, 0, indentSize);

    return stringBuilder.ToString();
  }

  private static void WriteExpression(SymbolicExpression symbolicExpression, StringBuilder writer, int indent, int indentSize) {
    switch (symbolicExpression) {
      case SymbolicExpressionAtom atom:
        WriteAtom(atom, writer);
        break;
      case SymbolicExpressionCollection collection:
        WriteCollection(collection, writer, indent, indentSize);
        break;
      default:
        throw new InvalidOperationException("Unknown symbolic expression type.");
    }
  }

  private static void WriteCollection(SymbolicExpressionCollection collection, StringBuilder writer, int indent, int indentSize) {
    writer.Append('(');

    if (collection.Children.Count == 0) {
      writer.Append(')');
      return;
    }

    foreach (var child in collection.Children) {
      writer.AppendLine();
      writer.Append(' ', (indent + 1) * indentSize);

      WriteExpression(child, writer, indent + 1, indentSize);
    }

    writer.AppendLine();
    writer.Append(' ', indent * indentSize);
    writer.Append(')');
  }

  private static void WriteAtom(SymbolicExpressionAtom atom, StringBuilder writer) {
    var bytes = atom.Value;

    if (IsPrintableToken(bytes)) {
      writer.Append(Encoding.ASCII.GetString(bytes));
      return;
    }

    writer.Append("#[");
    writer.Append(bytes.Length);
    writer.Append("] ");

    writer.Append(Convert.ToHexString(bytes));
  }

  private static bool IsPrintableToken(ReadOnlySpan<byte> bytes) {
    if (bytes.IsEmpty) {
      return false;
    }

    foreach (var b in bytes) {
      if (b is >= Characters.LOWER_A and <= Characters.LOWER_Z or
               >= Characters.UPPER_A and <= Characters.UPPER_Z or
               >= Characters.DIGIT_ZERO and <= Characters.DIGIT_NINE or
               Characters.HYPHEN or Characters.UNDERSCORE or Characters.PERIOD or Characters.SLASH or
               Characters.COLON or Characters.EQUALS or Characters.QUESTION_MARK or Characters.ASTERISK) {
        continue;
      }

      return false;
    }

    return true;
  }
}
