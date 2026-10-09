// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.RegularExpressions;
using AssuanLibrary.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AssuanLibrary.Tests.Documentation;

public sealed partial class SnippetTests {
  [Fact]
  public void NuGetAndGettingStartedSnippetsCompileAgainstPublicRuntime() {
    var root = ManifestTests._repository;
    foreach (var path in new[] { "docs/nuget/README.md", "docs/guides/getting-started.md" }) {
      var content = File.ReadAllText(Path.Combine(root, path));
      var snippet = Assert.Single(SnippetRegex().Matches(content)).Groups[1].Value;
      Assert.Empty(Errors(snippet));
    }
  }

  [Fact]
  public void CompiledExampleSnippetMatchesPublishedPrimitiveWorkflow() {
    var root = ManifestTests._repository;
    var code = File.ReadAllText(Path.Combine(root, "examples/AssuanLibrary.Examples/SExpressionExample.cs"));
    var start = code.IndexOf("// <snippet:sexpressions>", StringComparison.Ordinal);
    var end = code.IndexOf("// </snippet:sexpressions>", StringComparison.Ordinal);
    Assert.True(start >= 0 && end > start);
    var excerpt = code[(start + "// <snippet:sexpressions>".Length)..end];
    Assert.Empty(Errors("using AssuanLibrary.SExpressions;\nusing System;\n" +
                        "System.IO.TextWriter output = Console.Out;\n" + excerpt));
  }

  [Fact]
  public void RemovedRootApiSnippetFailsCompilation()
    => Assert.NotEmpty(Errors("using AssuanLibrary;\nvar oldClient = new AssuanClient();"));

  private static Diagnostic[] Errors(string source) {
    var runtime = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
    var refs = runtime.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToList();
    refs.Add(MetadataReference.CreateFromFile(typeof(ProtocolCommand).Assembly.Location));
    var parsed = CSharpSyntaxTree.ParseText(source,
      CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));
    var compilation = CSharpCompilation.Create("DocumentationSnippet", [parsed], refs,
      new CSharpCompilationOptions(OutputKind.ConsoleApplication));
    return compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
  }

  [GeneratedRegex(@"```csharp\s*\r?\n(.*?)\r?\n```", RegexOptions.Singleline)]
  private static partial Regex SnippetRegex();
}
