// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using System.Xml.Linq;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.CI;

public sealed class WorkflowContractTests {
  private static readonly string _repository = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);

  [Fact]
  public void SdkPinKeepsTheTestingPlatformRunner() {
    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_repository, "global.json")));
    Assert.Equal("10.0.400", json.RootElement.GetProperty("sdk").GetProperty("version").GetString());
    Assert.Equal("disable", json.RootElement.GetProperty("sdk").GetProperty("rollForward").GetString());
    Assert.Equal("Microsoft.Testing.Platform",
      json.RootElement.GetProperty("test").GetProperty("runner").GetString());
  }

  [Fact]
  public void SolutionRestoresFromVersionedPackageLocks() {
    var properties = XDocument.Load(Path.Combine(_repository, "Directory.Build.props"));
    Assert.Contains(properties.Descendants(), element => element.Name.LocalName == "RestorePackagesWithLockFile" &&
      element.Value == "true");

    foreach (var project in new[] {
      "src/AssuanLibrary.Generators", "src/AssuanLibrary",
      "examples/AssuanLibrary.Examples", "tools/AssuanLibrary.Docs",
      "tests/AssuanLibrary.GpgconfFixture", "tests/AssuanLibrary.Generators.Tests",
      "tests/AssuanLibrary.Tests"
    }) {
      var path = Path.Combine(_repository, project, "packages.lock.json");
      using var document = JsonDocument.Parse(File.ReadAllText(path));
      Assert.True(document.RootElement.GetProperty("version").GetInt32() >= 1);
      Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.GetProperty("dependencies").ValueKind);
    }
  }
}
