// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.SExpressions;

namespace AssuanLibrary.Examples;

internal sealed class SExpressionExample : IExample {
  public string Name => "sexpressions";
  public string Description => "Inspect atoms and round-trip a canonical binary S-expression";

  public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    if (args.Length != 0) {
      await error.WriteLineAsync("sexpressions takes no arguments.").ConfigureAwait(false);
      return 2;
    }

    token.ThrowIfCancellationRequested();
    // <snippet:sexpressions>
    var view = SExpressionParser.ParseComplete("(3:foo0:)"u8);
    // The view and its children borrow the input, so they cannot live across an await:
    // format every line first, then write.
    var lines = new List<string>();
    foreach (var child in view.Children) {
      var atom = child.AtomBytes.IsEmpty ? "<empty>" : $"0x{Convert.ToHexString(child.AtomBytes)}";
      lines.Add(string.Create(CultureInfo.InvariantCulture, $"sexpressions: atom[{lines.Count + 1}]={atom}"));
    }

    var owned = view.ToOwned();
    foreach (var line in lines) await output.WriteLineAsync(line).ConfigureAwait(false);

    await output
      .WriteLineAsync($"sexpressions: canonical=0x{Convert.ToHexString(SExpressionWriter.ToCanonical(owned))}")
      .ConfigureAwait(false);
    // </snippet:sexpressions>
    return 0;
  }
}
