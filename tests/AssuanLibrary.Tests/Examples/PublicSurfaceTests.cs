// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Tests.Examples;

public sealed class PublicSurfaceTests {
  private static readonly string _repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

  [Fact]
  public void RuntimeAssemblyExportsOnlyResponsibilityNamespaces() {
    var names = new HashSet<string>(StringComparer.Ordinal) {
      "AssuanLibrary.SExpressions", "AssuanLibrary.Protocol", "AssuanLibrary.Transport",
      "AssuanLibrary.Transport.Discovery", "AssuanLibrary.Transport.Unix", "AssuanLibrary.Transport.Windows",
      "AssuanLibrary.Server", "AssuanLibrary.Client", "AssuanLibrary.Attributes", "AssuanLibrary.Diagnostics"
    };
    var exports = typeof(ProtocolCommand).Assembly.GetExportedTypes();
    Assert.DoesNotContain(exports, type => type.Namespace == "AssuanLibrary" ||
                                           (type.Name.Contains("Native", StringComparison.Ordinal) && type.Name != "AgentEndpointKind"));
    Assert.All(exports, type => Assert.Contains(Assert.IsType<string>(type.Namespace), names));
  }

  [Fact]
  public void OneUserExamplesProjectIsPartOfTheSolution() {
    Assert.True(File.Exists(Path.Combine(_repository, "examples", "AssuanLibrary.Examples",
      "AssuanLibrary.Examples.csproj")));
    Assert.Contains("examples/AssuanLibrary.Examples/AssuanLibrary.Examples.csproj",
      File.ReadAllText(Path.Combine(_repository, "AssuanLibrary.slnx")));
  }
}
