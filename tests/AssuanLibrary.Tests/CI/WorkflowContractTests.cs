// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AssuanLibrary.Tests.CI;

public sealed partial class WorkflowContractTests {
  private readonly static string Repository = RepositoryRoot.Path;

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*[^\s#]+@(?<ref>[^\s#]+)", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex UsesRegex { get; }

  [GeneratedRegex("Assert-CheckoutSha\\.ps1", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex CmdletRegex { get; }

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*actions/checkout@", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex CheckoutRegex { get; }

  [Fact]
  public void SdkPinKeepsTheTestingPlatformRunner() {
    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository, "global.json")));
    Assert.Equal("10.0.403", json.RootElement.GetProperty("sdk").GetProperty("version").GetString());
    Assert.Equal("latestPatch", json.RootElement.GetProperty("sdk").GetProperty("rollForward").GetString());
    Assert.Equal("Microsoft.Testing.Platform",
      json.RootElement.GetProperty("test").GetProperty("runner").GetString());
  }

  [Fact]
  public void SolutionRestoresFromVersionedPackageLocks() {
    var properties = XDocument.Load(Path.Combine(Repository, "Directory.Build.props"));
    Assert.Contains(properties.Descendants(), element
      => string.Equals(element.Name.LocalName, "RestorePackagesWithLockFile", StringComparison.Ordinal) &&
         string.Equals(element.Value, "true", StringComparison.Ordinal));

    foreach (var project in new[] {
               "src/AssuanLibrary.Generators", "src/AssuanLibrary",
               "examples/AssuanLibrary.Examples",
               "tests/AssuanLibrary.GpgconfFixture", "tests/AssuanLibrary.Generators.Tests",
               "tests/AssuanLibrary.Tests"
             }) {
      var path = Path.Combine(Repository, project, "packages.lock.json");
      using var document = JsonDocument.Parse(File.ReadAllText(path));
      Assert.True(document.RootElement.GetProperty("version").GetInt32() >= 1);
      Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.GetProperty("dependencies").ValueKind);
    }
  }

  [Fact]
  public void CiWorkflowChecksAnExactShaWithoutPublicationCredentials() {
    var path = Path.Combine(Repository, ".github", "workflows", "ci.yml");
    var yaml = File.ReadAllText(path);
    Assert.Contains("workflow_call:", yaml, StringComparison.Ordinal);
    Assert.Contains("source_sha:", yaml, StringComparison.Ordinal);
    Assert.Contains("required: true", yaml, StringComparison.Ordinal);
    Assert.Contains("contents: read", yaml, StringComparison.Ordinal);
    Assert.Contains("persist-credentials: false", yaml, StringComparison.Ordinal);
    Assert.Contains("Assert-CheckoutSha.ps1", yaml, StringComparison.Ordinal);
    Assert.Contains("CALL_INPUTS:", yaml, StringComparison.Ordinal);
    Assert.Contains("-CallInputsJson", yaml, StringComparison.Ordinal);
    Assert.Contains("$call.Contains('source_sha')", yaml, StringComparison.Ordinal);
    Assert.Contains("Assert-Gate.ps1", yaml, StringComparison.Ordinal);
    Assert.Contains("if: always()", yaml, StringComparison.Ordinal);
    Assert.Contains("needs: [prepare, matrix, package, format, workflows]", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("docfx", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("tools/AssuanLibrary.Docs", yaml, StringComparison.Ordinal);
    Assert.Contains("dotnet restore AssuanLibrary.slnx --locked-mode", yaml, StringComparison.Ordinal);
    // The ReSharper tools are the formatter of record; dotnet format disagrees with their options.
    Assert.Contains("./scripts/ci/Verify-Format.ps1", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("dotnet format", yaml, StringComparison.Ordinal);
    var check = File.ReadAllText(Path.Combine(Repository, "scripts", "ci", "Verify-Format.ps1"));
    Assert.Contains("dotnet tool restore", check, StringComparison.Ordinal);
    Assert.Contains("cleanupcode", check, StringComparison.Ordinal);
    Assert.Contains("inspectcode", check, StringComparison.Ordinal);
    var tools = File.ReadAllText(Path.Combine(Repository, ".config", "dotnet-tools.json"));
    Assert.Contains("jetbrains.resharper.globaltools", tools, StringComparison.Ordinal);
    Assert.Contains("--filter-class", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("secrets: inherit", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("nuget push", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("pages: write", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("contents: write", yaml, StringComparison.OrdinalIgnoreCase);
    var actions = UsesRegex.Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups["ref"].Value));
    Assert.Equal(6, CmdletRegex.Count(yaml));
    Assert.Equal(6, CheckoutRegex.Count(yaml));
    Assert.False(File.Exists(Path.Combine(Repository, ".github", "workflows", "docs.yml")));
  }

  [Fact]
  public void DependencyUpdatesTargetTheIntegrationBranchAndLeaveManualPinsAlone() {
    var yaml = File.ReadAllText(Path.Combine(Repository, ".github", "dependabot.yml"))
      .Replace("\r\n", "\n", StringComparison.Ordinal);
    Assert.Contains("version: 2", yaml, StringComparison.Ordinal);
    Assert.Contains("package-ecosystem: nuget", yaml, StringComparison.Ordinal);
    Assert.Contains("package-ecosystem: github-actions", yaml, StringComparison.Ordinal);
    Assert.Equal(2, yaml.Split("package-ecosystem:").Length - 1);
    Assert.Equal(2, yaml.Split("target-branch: develop").Length - 1);
    Assert.Contains("prefix: build(deps)", yaml, StringComparison.Ordinal);
    Assert.Contains("prefix: ci(deps)", yaml, StringComparison.Ordinal);
    // The SDK pin is asserted above and documented for contributors, so it stays a manual change.
    Assert.DoesNotContain("package-ecosystem: dotnet-sdk", yaml, StringComparison.Ordinal);
    Assert.Contains("dependency-name: Microsoft.CodeAnalysis.CSharp", yaml, StringComparison.Ordinal);
    Assert.True(File.Exists(Path.Combine(Repository, "SECURITY.md")));
  }

  [Theory]
  [InlineData("ci.yml")]
  [InlineData("interop.yml")]
  public void BranchPushesAreValidatedOnceThroughTheirPullRequest(string workflow) {
    var yaml = File.ReadAllText(Path.Combine(Repository, ".github", "workflows", workflow))
      .Replace("\r\n", "\n", StringComparison.Ordinal);
    Assert.Contains("  push:\n    branches: [main, develop]\n  pull_request:\n", yaml, StringComparison.Ordinal);
  }

  [Fact]
  public void CiGuideAndWorkflowValidatorStayInTheRepository() {
    var guide = File.ReadAllText(Path.Combine(Repository, "CONTRIBUTING.md"));
    Assert.Contains("## Continuous integration", guide, StringComparison.Ordinal);
    Assert.Contains("Verify-Workflows.ps1", guide, StringComparison.Ordinal);
    Assert.True(File.Exists(Path.Combine(Repository, "scripts", "ci", "Verify-Workflows.ps1")));
  }

  [Fact]
  public void InteropWorkflowRequiresRealGnuPgOnEveryHostedPlatform() {
    var yaml = File.ReadAllText(Path.Combine(Repository, ".github", "workflows", "interop.yml"));
    Assert.Contains("workflow_call:", yaml, StringComparison.Ordinal);
    Assert.Contains("source_sha:", yaml, StringComparison.Ordinal);
    Assert.Contains("required: true", yaml, StringComparison.Ordinal);
    Assert.Contains("os: [ubuntu-latest, windows-latest, macos-latest]", yaml, StringComparison.Ordinal);
    Assert.Contains("ASSUAN_GNUPG_REQUIRED: '1'", yaml, StringComparison.Ordinal);
    Assert.Contains("CALL_INPUTS:", yaml, StringComparison.Ordinal);
    Assert.Contains("-CallInputsJson", yaml, StringComparison.Ordinal);
    Assert.Contains("$call.Contains('source_sha')", yaml, StringComparison.Ordinal);
    Assert.Contains("--filter-namespace AssuanLibrary.Tests.Interop", yaml, StringComparison.Ordinal);
    Assert.Contains("ASSUAN_GPGCONF_PATH", yaml, StringComparison.Ordinal);
    Assert.Contains("ASSUAN_GPG_AGENT_PATH", yaml, StringComparison.Ordinal);
    Assert.Contains("ASSUAN_GPG_CONNECT_AGENT_PATH", yaml, StringComparison.Ordinal);
    Assert.Contains("scoop install main/gnupg", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("get.scoop.sh", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.Matches(@"\$installerCommit = '[0-9a-f]{40}'", yaml);
    Assert.Matches(@"\$installerSha256 = '[0-9a-f]{64}'", yaml);
    Assert.Contains("Scoop installer checksum mismatch.", yaml, StringComparison.Ordinal);
    Assert.Contains(@"apps\gnupg\current\bin", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("choco install", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("persist-credentials: false", yaml, StringComparison.Ordinal);
    Assert.Contains("if: always()", yaml, StringComparison.Ordinal);
    Assert.Contains("needs: [prepare, real]", yaml, StringComparison.Ordinal);
    Assert.Contains("Assert-Gate.ps1", yaml, StringComparison.Ordinal);
    Assert.DoesNotContain("secrets: inherit", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("contents: write", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("nuget push", yaml, StringComparison.OrdinalIgnoreCase);
    var actions = UsesRegex.Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups["ref"].Value));
    Assert.Equal(3, CmdletRegex.Count(yaml));
  }
}
