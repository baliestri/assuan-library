// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AssuanLibrary.Generators.Tests;

internal static class GeneratorHarness {
  internal static Result Compile(string source, bool includeGenerator = true,
  params MetadataReference[] extraReferences) {
    var parse = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
    var runtime = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
                  ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
    var references = runtime.Split(Path.PathSeparator)
      .Where(path => !Path.GetFileName(path).StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase))
      .Select(MetadataReference (path) => MetadataReference.CreateFromFile(path))
      .ToList();
    references.Add(MetadataReference.CreateFromFile(typeof(AssuanCommandHandlerAttribute).Assembly.Location));
    references.AddRange(extraReferences);
    var compilation = CSharpCompilation.Create("GeneratorConsumer",
      [CSharpSyntaxTree.ParseText(source, parse)], references,
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    if (!includeGenerator) {
      return new Result([], [], [],
        compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray(), compilation);
    }

    GeneratorDriver driver = CSharpGeneratorDriver.Create(
      [new AssuanIncrementalGenerator().AsSourceGenerator()], parseOptions: parse);
    driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
    var run = driver.GetRunResult();
    var produced = run.Results.SelectMany(result => result.GeneratedSources).ToArray();
    return new Result(produced.Select(item => item.SourceText.ToString()).ToArray(),
      produced.Select(item => item.HintName).ToArray(), diagnostics.ToArray(),
      output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray(),
      (CSharpCompilation)output);
  }

  internal sealed record Result(string[] GeneratedSources,
    string[] GeneratedHints,
    Diagnostic[] GeneratorDiagnostics,
    Diagnostic[] OutputCompilationErrors,
    CSharpCompilation OutputCompilation);
}
