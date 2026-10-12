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
               "prepared SHA", "recovery", "docs-v<version>.zip", "version=2.0.1"
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
    var yaml = ReadWorkflow("release.yml");
    Assert.Contains("workflow_dispatch:", yaml);
    Assert.Contains("resume_sha:", yaml);
    Assert.Contains("steps.resume.outputs.prepared_sha", yaml);
    Assert.Contains("if: inputs.resume_sha != ''", yaml);
    Assert.Contains("Assert-ResumeCandidate.ps1", yaml);
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
    Assert.Contains("docs-v${{ inputs.version }}.zip", yaml);
    Assert.Contains("RELEASE_VERSION: ${{ inputs.version }}", yaml);
    Assert.Contains("-Version $env:RELEASE_VERSION", yaml);
    Assert.Contains("git push --atomic", yaml);
    Assert.DoesNotContain("release/v2.0.0", yaml);
    Assert.DoesNotContain("assuanlibrary.2.0.0.nupkg", yaml);
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

  [Fact]
  public void PagesWorkflowBuildsReadOnlyAndDeploysWithSeparatePermissions() {
    var yaml = ReadWorkflow("docs.yml");
    Assert.Contains("workflow_dispatch:", yaml);
    Assert.Contains("workflow_call:", yaml);
    Assert.Contains("CALL_INPUTS:", yaml);
    Assert.Contains("$call.Contains('source_sha')", yaml);
    Assert.Contains("cancel-in-progress: false", yaml);
    Assert.Contains("Collect-Pages.ps1", yaml);
    Assert.Contains("Assemble-Pages.ps1", yaml);
    Assert.Contains("upload-pages-artifact@", yaml);
    Assert.Contains("deploy-pages@", yaml);
    var deployStart = yaml.IndexOf("\n  deploy:\n", StringComparison.Ordinal);
    Assert.DoesNotContain("pages: write", yaml[..deployStart]);
    Assert.DoesNotContain("id-token: write", yaml[..deployStart]);
    Assert.Contains("pages: write", yaml[deployStart..]);
    Assert.Contains("id-token: write", yaml[deployStart..]);
    Assert.DoesNotContain("nuget push", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("dotnet pack", yaml, StringComparison.OrdinalIgnoreCase);
    var actions = UsesRegex().Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups[1].Value));
  }

  [Fact]
  public void LocalReleaseRunnerCannotPublishOrAdvanceRefs() {
    var script = File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Test-Release.ps1"));
    Assert.Contains("artifacts/release-tests", script);
    Assert.Contains("--locked-mode", script);
    Assert.Contains("--verify-no-changes", script);
    Assert.Contains("Verify-Workflows.ps1", script);
    Assert.DoesNotContain("git push", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("nuget push", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("gh release create", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("gh workflow run", script, StringComparison.OrdinalIgnoreCase);
  }

  [GeneratedRegex("id-token: write")]
  private static partial Regex IdTokenWriteRegex();

  private static string ReadWorkflow(string name)
    => File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, ".github", "workflows", name))
      .Replace("\r\n", "\n", StringComparison.Ordinal);

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*[^\s#]+@([^\s#]+)")]
  private static partial Regex UsesRegex();
}
