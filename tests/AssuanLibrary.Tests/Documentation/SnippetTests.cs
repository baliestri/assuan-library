// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text.RegularExpressions;
using AssuanLibrary.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AssuanLibrary.Tests.Documentation;

public sealed partial class SnippetTests {
  [GeneratedRegex(@"```csharp\s*\r?\n(?<code>.*?)\r?\n```", RegexOptions.Singleline | RegexOptions.ExplicitCapture,
    5000)]
  private static partial Regex SnippetRegex { get; }

  [Fact]
  public void ReadmePackageAndGettingStartedSnippetsCompileAgainstPublicRuntime() {
    var root = RepositoryRoot.Path;
    foreach (var path in new[] { "README.md", "src/AssuanLibrary/PACKAGE.md", "wiki/Getting-Started.md" }) {
      var content = File.ReadAllText(Path.Combine(root, path));
      var snippet = Assert.Single(SnippetRegex.Matches(content)).Groups["code"].Value;
      Assert.Empty(Errors(snippet));
    }
  }

  [Fact]
  public void CompiledExampleSnippetMatchesPublishedPrimitiveWorkflow() {
    var root = RepositoryRoot.Path;
    var code = File.ReadAllText(Path.Combine(root, "examples/AssuanLibrary.Examples/SExpressionExample.cs"));
    var start = code.IndexOf("// <snippet:sexpressions>", StringComparison.Ordinal);
    var end = code.IndexOf("// </snippet:sexpressions>", StringComparison.Ordinal);
    Assert.True(start >= 0 && end > start);
    var excerpt = code[(start + "// <snippet:sexpressions>".Length)..end];
    Assert.Empty(Errors(
      "using AssuanLibrary.SExpressions;\nusing System;\nusing System.Collections.Generic;\n" +
      $"using System.Globalization;\nSystem.IO.TextWriter output = Console.Out;\n{excerpt}"));
  }

  [Fact]
  public void RemovedRootApiSnippetFailsCompilation()
    => Assert.NotEmpty(Errors("using AssuanLibrary;\nvar oldClient = new AssuanClient();"));

  private static Diagnostic[] Errors(string source) {
    var runtime = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
    var refs = runtime.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToList();
    refs.Add(MetadataReference.CreateFromFile(typeof(ProtocolCommand).Assembly.Location));
    var parsed = CSharpSyntaxTree.ParseText(source,
      CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
      cancellationToken: TestContext.Current.CancellationToken);
    var compilation = CSharpCompilation.Create("DocumentationSnippet", [parsed], refs,
      new CSharpCompilationOptions(OutputKind.ConsoleApplication));
    return compilation.GetDiagnostics(TestContext.Current.CancellationToken)
      .Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
  }
}
