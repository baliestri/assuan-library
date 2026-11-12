// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssuanLibrary.Tests.Release;

public sealed partial class WorkflowTests {
  [GeneratedRegex("id-token: write", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex IdTokenWriteRegex { get; }

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*[^\s#]+@(?<ref>[^\s#]+)", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex UsesRegex { get; }

  [Fact]
  public void ContributorGuideDescribesTheCurrentSolutionAndChecks() {
    var text = File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, "CONTRIBUTING.adoc"));
    Assert.Contains("AssuanLibrary.slnx", text, StringComparison.Ordinal);
    Assert.Contains("net10.0", text, StringComparison.Ordinal);
    Assert.Contains("netstandard2.0", text, StringComparison.Ordinal);
    Assert.Contains("--locked-mode", text, StringComparison.Ordinal);
    Assert.Contains("dotnet format", text, StringComparison.Ordinal);
    Assert.Contains("docs/nuget/README.md", text, StringComparison.Ordinal);
    Assert.Contains("git-cliff", text, StringComparison.Ordinal);
    Assert.Contains("macOS", text, StringComparison.Ordinal);
    Assert.DoesNotContain("AssuanLibrary.Sample", text, StringComparison.Ordinal);
    Assert.DoesNotContain("AssuanLibrary.Extensions.DependencyInjection", text, StringComparison.Ordinal);
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
             })
      Assert.Contains(value, guide, StringComparison.OrdinalIgnoreCase);

    Assert.Contains("-DryRun", guide, StringComparison.Ordinal);
    using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "site", "sources.json")));
    var entry = Assert.Single(manifest.RootElement.GetProperty("sources").EnumerateArray(),
      item => string.Equals(item.GetProperty("source").GetString(), "docs/guides/releasing.md",
        StringComparison.Ordinal));
    Assert.True(entry.GetProperty("includeInIndex").GetBoolean());
    Assert.False(entry.GetProperty("includeInFull").GetBoolean());
  }

  [Fact]
  public void ManualReleaseUsesMatchingGatesAndOneProtectedOidcPublication() {
    var yaml = ReadWorkflow("release.yml");
    Assert.Contains("workflow_dispatch:", yaml, StringComparison.Ordinal);
    Assert.Contains("resume_sha:", yaml, StringComparison.Ordinal);
    Assert.Contains("steps.resume.outputs.prepared_sha", yaml, StringComparison.Ordinal);
    Assert.Contains("if: inputs.resume_sha != ''", yaml, StringComparison.Ordinal);
    Assert.Contains("Assert-ResumeCandidate.ps1", yaml, StringComparison.Ordinal);
    Assert.Contains("cancel-in-progress: false", yaml, StringComparison.Ordinal);
    Assert.Contains("./.github/workflows/ci.yml", yaml, StringComparison.Ordinal);
    Assert.Contains("./.github/workflows/interop.yml", yaml, StringComparison.Ordinal);
    Assert.Contains("needs.ci.outputs.source_sha", yaml, StringComparison.Ordinal);
    Assert.Contains("needs.interop.outputs.source_sha", yaml, StringComparison.Ordinal);
    Assert.Contains("Verify-Release.ps1", yaml, StringComparison.Ordinal);
    Assert.Contains("Build-ReleaseArtifacts.ps1", yaml, StringComparison.Ordinal);
    Assert.Contains("environment: release", yaml, StringComparison.Ordinal);
    Assert.Contains("id-token: write", yaml, StringComparison.Ordinal);
    Assert.Contains("NuGet/login@", yaml, StringComparison.Ordinal);
    Assert.Contains("docs-v${{ inputs.version }}.zip", yaml, StringComparison.Ordinal);
    Assert.Contains("RELEASE_VERSION: ${{ inputs.version }}", yaml, StringComparison.Ordinal);
    Assert.Contains("-Version $env:RELEASE_VERSION", yaml, StringComparison.Ordinal);
    Assert.Contains("git push --atomic", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("release/v2.0.0", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("assuanlibrary.2.0.0.nupkg", yaml, StringComparison.Ordinal);
    Assert.Contains("--atomic", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("--force", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("--skip-duplicate", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("secrets: inherit", yaml, StringComparison.OrdinalIgnoreCase);
    var publishStart = yaml.IndexOf("\n  publish:\n", StringComparison.Ordinal);
    var syncStart = yaml.IndexOf("\n  sync:\n", StringComparison.Ordinal);
    var beforePublish = yaml[..publishStart];
    var publish = yaml[publishStart..syncStart];
    Assert.DoesNotContain("id-token: write", beforePublish, StringComparison.Ordinal);
    Assert.DoesNotContain("nuget push", beforePublish, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("id-token: write", publish, StringComparison.Ordinal);
    Assert.Contains("download-artifact@", publish, StringComparison.Ordinal);
    Assert.Single(IdTokenWriteRegex.Matches(yaml));
    var actions = UsesRegex.Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups["ref"].Value));
  }

  [Fact]
  public void PagesWorkflowBuildsReadOnlyAndDeploysWithSeparatePermissions() {
    var yaml = ReadWorkflow("docs.yml");
    Assert.Contains("workflow_dispatch:", yaml, StringComparison.Ordinal);
    Assert.Contains("workflow_call:", yaml, StringComparison.Ordinal);
    Assert.Contains("CALL_INPUTS:", yaml, StringComparison.Ordinal);
    Assert.Contains("$call.Contains('source_sha')", yaml, StringComparison.Ordinal);
    Assert.Contains("cancel-in-progress: false", yaml, StringComparison.Ordinal);
    Assert.Contains("Collect-Pages.ps1", yaml, StringComparison.Ordinal);
    Assert.Contains("Assemble-Pages.ps1", yaml, StringComparison.Ordinal);
    Assert.Contains("upload-pages-artifact@", yaml, StringComparison.Ordinal);
    Assert.Contains("deploy-pages@", yaml, StringComparison.Ordinal);
    var deployStart = yaml.IndexOf("\n  deploy:\n", StringComparison.Ordinal);
    Assert.DoesNotContain("pages: write", yaml[..deployStart], StringComparison.Ordinal);
    Assert.DoesNotContain("id-token: write", yaml[..deployStart], StringComparison.Ordinal);
    Assert.Contains("pages: write", yaml[deployStart..], StringComparison.Ordinal);
    Assert.Contains("id-token: write", yaml[deployStart..], StringComparison.Ordinal);
    Assert.DoesNotContain("nuget push", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("dotnet pack", yaml, StringComparison.OrdinalIgnoreCase);
    var actions = UsesRegex.Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups["ref"].Value));
  }

  [Fact]
  public void LocalReleaseRunnerCannotPublishOrAdvanceRefs() {
    var script = File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Test-Release.ps1"));
    Assert.Contains("artifacts/release-tests", script, StringComparison.Ordinal);
    Assert.Contains("--locked-mode", script, StringComparison.Ordinal);
    Assert.Contains("--verify-no-changes", script, StringComparison.Ordinal);
    Assert.Contains("Verify-Workflows.ps1", script, StringComparison.Ordinal);
    Assert.DoesNotContain("git push", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("nuget push", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("gh release create", script, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("gh workflow run", script, StringComparison.OrdinalIgnoreCase);
  }

  private static string ReadWorkflow(string name)
    => File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, ".github", "workflows", name))
      .Replace("\r\n", "\n", StringComparison.Ordinal);
}
