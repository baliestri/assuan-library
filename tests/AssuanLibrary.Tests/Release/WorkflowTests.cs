// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssuanLibrary.Tests.Release;

public sealed partial class WorkflowTests {
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

  [Fact]
  public void ManualReleaseUsesMatchingGatesAndOneProtectedOidcPublication() {
    var yaml = File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, ".github", "workflows", "release.yml"));
    Assert.Contains("workflow_dispatch:", yaml);
    Assert.Contains("cancel-in-progress: false", yaml);
    Assert.Contains("./.github/workflows/ci.yml", yaml);
    Assert.Contains("./.github/workflows/interop.yml", yaml);
    Assert.Contains("needs.ci.outputs.source_sha", yaml);
    Assert.Contains("needs.interop.outputs.source_sha", yaml);
    Assert.Contains("Verify-Release.ps1", yaml);
    Assert.Contains("Build-ReleaseArtifacts.ps1", yaml);
    Assert.Contains("environment: release", yaml);
    Assert.Contains("id-token: write", yaml);
    Assert.Contains("NuGet/login@", yaml);
    Assert.Contains("docs-v2.0.0.zip", yaml);
    Assert.Contains("--atomic", yaml);
    Assert.DoesNotContain("--force", yaml);
    Assert.DoesNotContain("--skip-duplicate", yaml);
    Assert.DoesNotContain("secrets: inherit", yaml, StringComparison.OrdinalIgnoreCase);
    var publishStart = yaml.IndexOf("\n  publish:\n", StringComparison.Ordinal);
    var syncStart = yaml.IndexOf("\n  sync:\n", StringComparison.Ordinal);
    var beforePublish = yaml[..publishStart];
    var publish = yaml[publishStart..syncStart];
    Assert.DoesNotContain("id-token: write", beforePublish);
    Assert.DoesNotContain("nuget push", beforePublish, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("id-token: write", publish);
    Assert.Contains("download-artifact@", publish);
    Assert.Single(IdTokenWriteRegex().Matches(yaml));
    var actions = UsesRegex().Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups[1].Value));
  }

  [GeneratedRegex("id-token: write")]
  private static partial Regex IdTokenWriteRegex();

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*[^\s#]+@([^\s#]+)")]
  private static partial Regex UsesRegex();
}
