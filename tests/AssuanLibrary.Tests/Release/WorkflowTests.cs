// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text.RegularExpressions;

namespace AssuanLibrary.Tests.Release;

public sealed partial class WorkflowTests {
  [GeneratedRegex("id-token: write", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex IdTokenWriteRegex { get; }

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*[^\s#]+@(?<ref>[^\s#]+)", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex UsesRegex { get; }

  [Fact]
  public void ContributorGuideDescribesTheCurrentSolutionAndChecks() {
    var text = File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, "CONTRIBUTING.md"));
    Assert.Contains("AssuanLibrary.slnx", text, StringComparison.Ordinal);
    Assert.Contains("net10.0", text, StringComparison.Ordinal);
    Assert.Contains("netstandard2.0", text, StringComparison.Ordinal);
    Assert.Contains("--locked-mode", text, StringComparison.Ordinal);
    Assert.Contains("Verify-Format.ps1", text, StringComparison.Ordinal);
    Assert.Contains("jb cleanupcode", text, StringComparison.Ordinal);
    Assert.DoesNotContain("--verify-no-changes", text, StringComparison.Ordinal);
    Assert.Contains("PACKAGE.md", text, StringComparison.Ordinal);
    Assert.Contains("git-cliff", text, StringComparison.Ordinal);
    Assert.Contains("macOS", text, StringComparison.Ordinal);
    Assert.DoesNotContain("AssuanLibrary.Sample", text, StringComparison.Ordinal);
    Assert.DoesNotContain("AssuanLibrary.Extensions.DependencyInjection", text, StringComparison.Ordinal);
    Assert.DoesNotMatch(@"AssuanLibrary\.sln(?!x)", text);
  }

  [Fact]
  public void ReleaseProcessIsDescribedForContributorsNotInTheWiki() {
    var root = ReleaseFixture.ToolsRoot;
    // A Windows checkout may convert line endings, so headings are located in normalized text.
    var guide = File.ReadAllText(Path.Combine(root, "CONTRIBUTING.md"))
      .Replace("\r\n", "\n", StringComparison.Ordinal);
    var start = guide.IndexOf("\n## Releasing\n", StringComparison.Ordinal);
    var end = guide.IndexOf("\n## Repository conventions\n", StringComparison.Ordinal);
    Assert.True(start > 0 && end > start);
    var releasing = guide[start..end];
    foreach (var value in new[] {
               "v2.0.0", "OIDC", "NuGet", "git-cliff", "prepared SHA", "recovery",
               "version=2.0.1", "Re-run failed jobs", "resume_sha", "attestation"
             })
      Assert.Contains(value, releasing, StringComparison.OrdinalIgnoreCase);

    Assert.Contains("-DryRun", releasing, StringComparison.Ordinal);
    // The site and its documentation archive no longer exist, and process pages stay out of the wiki.
    Assert.DoesNotContain("Pages", releasing, StringComparison.Ordinal);
    Assert.DoesNotContain("docs-v", releasing, StringComparison.Ordinal);
    Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "wiki"), "*eleas*"));
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
    Assert.Contains("AssuanLibrary.${{ inputs.version }}.nupkg", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("docs-v", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("docs.yml", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("actions: write", yaml, StringComparison.Ordinal);
    Assert.Contains("RELEASE_VERSION: ${{ inputs.version }}", yaml, StringComparison.Ordinal);
    Assert.Contains("-Version $env:RELEASE_VERSION", yaml, StringComparison.Ordinal);
    Assert.Contains("git push --atomic", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("release/v2.0.0", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("assuanlibrary.2.0.0.nupkg", yaml, StringComparison.Ordinal);
    Assert.Contains("--atomic", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("--force", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("--skip-duplicate", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("secrets: inherit", yaml, StringComparison.OrdinalIgnoreCase);
    // Exactly two jobs may request an OIDC token: provenance attestation and the protected upload.
    Assert.Equal(2, IdTokenWriteRegex.Count(yaml));
    var actions = UsesRegex.Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups["ref"].Value));
  }

  [Fact]
  public void ProvenanceIsAttestedByAJobWithoutReleaseOrRepositoryWriteAccess() {
    var yaml = ReadWorkflow("release.yml");
    var attestStart = yaml.IndexOf("\n  attest:\n", StringComparison.Ordinal);
    var promoteStart = yaml.IndexOf("\n  promote:\n", StringComparison.Ordinal);
    var publishStart = yaml.IndexOf("\n  publish:\n", StringComparison.Ordinal);
    Assert.True(attestStart > 0 && promoteStart > attestStart && publishStart > promoteStart);
    var attest = yaml[attestStart..promoteStart];
    Assert.Contains("needs: [prepare, build]", attest, StringComparison.Ordinal);
    Assert.Contains("attestations: write", attest, StringComparison.Ordinal);
    Assert.Contains("id-token: write", attest, StringComparison.Ordinal);
    Assert.Contains("contents: read", attest, StringComparison.Ordinal);
    Assert.Contains("attest-build-provenance@", attest, StringComparison.Ordinal);
    Assert.Contains("download-artifact@", attest, StringComparison.Ordinal);
    Assert.Contains("AssuanLibrary.${{ inputs.version }}.nupkg", attest, StringComparison.Ordinal);
    Assert.DoesNotContain("environment:", attest, StringComparison.Ordinal);
    Assert.DoesNotContain("contents: write", attest, StringComparison.Ordinal);
    Assert.DoesNotContain("checkout@", attest, StringComparison.Ordinal);
    Assert.DoesNotContain("run:", attest, StringComparison.Ordinal);
    Assert.DoesNotContain("NuGet/login@", attest, StringComparison.Ordinal);
    // No other job before the protected upload can obtain an OIDC token.
    Assert.DoesNotContain("id-token: write", yaml[..attestStart], StringComparison.Ordinal);
    Assert.DoesNotContain("id-token: write", yaml[promoteStart..publishStart], StringComparison.Ordinal);
    Assert.Equal(1, yaml.Split("attestations: write").Length - 1);
    Assert.Contains("needs: [prepare, ci, interop, build, attest]", yaml[promoteStart..publishStart],
      StringComparison.Ordinal);
  }

  [Fact]
  public void RegistryConfirmationIsASeparateRerunnableJobThatCannotUpload() {
    var yaml = ReadWorkflow("release.yml");
    var publishStart = yaml.IndexOf("\n  publish:\n", StringComparison.Ordinal);
    var confirmStart = yaml.IndexOf("\n  confirm:\n", StringComparison.Ordinal);
    var syncStart = yaml.IndexOf("\n  sync:\n", StringComparison.Ordinal);
    Assert.True(publishStart > 0 && confirmStart > publishStart && syncStart > confirmStart);
    var beforePublish = yaml[..publishStart];
    var publish = yaml[publishStart..confirmStart];
    var confirm = yaml[confirmStart..syncStart];
    Assert.DoesNotContain("nuget push", beforePublish, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("environment: release", beforePublish, StringComparison.Ordinal);
    Assert.Contains("id-token: write", publish, StringComparison.Ordinal);
    Assert.Contains("download-artifact@", publish, StringComparison.Ordinal);
    Assert.Contains("nuget push", publish, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("gh release", publish, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("contents: write", publish, StringComparison.Ordinal);
    // Registry visibility is awaited in a separately re-runnable job that can never upload.
    Assert.DoesNotContain("nuget push", confirm, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("id-token", confirm, StringComparison.Ordinal);
    Assert.DoesNotContain("NuGet/login@", confirm, StringComparison.Ordinal);
    Assert.DoesNotContain("environment: release", confirm, StringComparison.Ordinal);
    Assert.Contains("needs: [prepare, ci, interop, publish]", confirm, StringComparison.Ordinal);
    Assert.Contains("Wait-RegistryPackage.ps1", confirm, StringComparison.Ordinal);
    Assert.Contains("Compare-RegistryPackage.ps1", confirm, StringComparison.Ordinal);
    Assert.Contains("download-artifact@", confirm, StringComparison.Ordinal);
    Assert.Contains("gh release create", confirm, StringComparison.Ordinal);
    Assert.Contains("\"AssuanLibrary.$($env:RELEASE_VERSION).nupkg\"", confirm, StringComparison.Ordinal);
    Assert.DoesNotContain("gh workflow run", confirm, StringComparison.Ordinal);
    Assert.Contains("needs: [prepare, confirm]", yaml[syncStart..], StringComparison.Ordinal);
  }

  [Fact]
  public void LocalReleaseRunnerCannotPublishOrAdvanceRefs() {
    var script = File.ReadAllText(Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Test-Release.ps1"));
    Assert.Contains("artifacts/release-tests", script, StringComparison.Ordinal);
    Assert.Contains("--locked-mode", script, StringComparison.Ordinal);
    Assert.Contains("Verify-Format.ps1", script, StringComparison.Ordinal);
    Assert.DoesNotContain("dotnet format", script, StringComparison.Ordinal);
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
