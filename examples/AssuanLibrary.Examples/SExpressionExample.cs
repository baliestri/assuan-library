// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.SExpressions;

namespace AssuanLibrary.Examples;

internal sealed class SExpressionExample : IExample {
  public string Name => "sexpressions";
  public string Description => "Inspect atoms and round-trip a canonical binary S-expression";

  public Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token) {
    if (args.Length != 0) {
      error.WriteLine("sexpressions takes no arguments.");
      return Task.FromResult(2);
    }

    token.ThrowIfCancellationRequested();
    // <snippet:sexpressions>
    var view = SExpressionParser.ParseComplete("(3:foo0:)"u8);
    var index = 0;
    foreach (var child in view.Children) {
      ++index;
      output.WriteLine($"sexpressions: atom[{index}]=" +
                       (child.AtomBytes.IsEmpty ? "<empty>" : "0x" + Convert.ToHexString(child.AtomBytes)));
    }

    var owned = view.ToOwned();
    output.WriteLine($"sexpressions: canonical=0x{Convert.ToHexString(SExpressionWriter.ToCanonical(owned))}");
    // </snippet:sexpressions>
    return Task.FromResult(0);
  }
}
