// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;

namespace AssuanLibrary.Tests.Release;

public sealed class WorkflowTests {
  [Fact]
  public void ContributorGuideDescribesTheCurrentSolutionAndChecks() {
    var text = File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, "CONTRIBUTING.adoc"));
    Assert.Contains("AssuanLibrary.slnx", text);
    Assert.Contains("net10.0", text);
    Assert.Contains("netstandard2.0", text);
    Assert.Contains("--locked-mode", text);
    Assert.Contains("dotnet format", text);
    Assert.Contains("docs/nuget/README.md", text);
    Assert.Contains("git-cliff", text);
    Assert.Contains("macOS", text);
    Assert.DoesNotContain("AssuanLibrary.Sample", text);
    Assert.DoesNotContain("AssuanLibrary.Extensions.DependencyInjection", text);
    Assert.DoesNotMatch(@"AssuanLibrary\.sln(?!x)", text);
    Assert.True(File.Exists(Path.Combine(ReleaseFixture.ToolsRoot, "docs", "guides", "releasing.md")));
  }

  [Fact]
  public void ReleaseGuideIsOperationalAndExcludedFromTheUserCorpus() {
    var root = ReleaseFixture.ToolsRoot;
    var guide = File.ReadAllText(Path.Combine(root, "docs", "guides", "releasing.md"));
    foreach (var value in new[] {
               "v2.0.0", "OIDC", "NuGet", "Pages", "git-cliff",
               "prepared SHA", "recovery", "docs-v2.0.0.zip"
             }) {
      Assert.Contains(value, guide, StringComparison.OrdinalIgnoreCase);
    }

    Assert.Contains("-DryRun", guide);
    using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "site", "sources.json")));
    var entry = Assert.Single(manifest.RootElement.GetProperty("sources").EnumerateArray(), item =>
      item.GetProperty("source").GetString() == "docs/guides/releasing.md");
    Assert.True(entry.GetProperty("includeInIndex").GetBoolean());
    Assert.False(entry.GetProperty("includeInFull").GetBoolean());
  }
}
