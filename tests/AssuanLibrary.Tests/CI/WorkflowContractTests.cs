// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.CI;

public sealed partial class WorkflowContractTests {
  private readonly static string Repository = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);

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
               "examples/AssuanLibrary.Examples", "tools/AssuanLibrary.Docs",
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
    Assert.Contains("workflow_call:", yaml);
    Assert.Contains("source_sha:", yaml);
    Assert.Contains("required: true", yaml);
    Assert.Contains("contents: read", yaml);
    Assert.Contains("persist-credentials: false", yaml);
    Assert.Contains("Assert-CheckoutSha.ps1", yaml);
    Assert.Contains("CALL_INPUTS:", yaml);
    Assert.Contains("-CallInputsJson", yaml);
    Assert.Contains("$call.Contains('source_sha')", yaml);
    Assert.Contains("Assert-Gate.ps1", yaml);
    Assert.Contains("if: always()", yaml);
    Assert.Contains("needs: [prepare, matrix, docs, format, workflows]", yaml);
    Assert.Contains("dotnet restore AssuanLibrary.slnx --locked-mode", yaml);
    Assert.Contains("dotnet format AssuanLibrary.slnx --verify-no-changes", yaml);
    Assert.Contains("--filter-class", yaml);
    Assert.DoesNotContain("secrets: inherit", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("nuget push", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("pages: write", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("contents: write", yaml, StringComparison.OrdinalIgnoreCase);
    var actions = UsesRegex().Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups["ref"].Value));
    Assert.Equal(6, CmdletRegex().Count(yaml));
    Assert.Equal(6, CheckoutRegex().Count(yaml));
  }

  [Fact]
  public void CiGuideAndWorkflowValidatorStayInTheRepository() {
    Assert.True(File.Exists(Path.Combine(Repository, "docs", "guides", "continuous-integration.md")));
    Assert.True(File.Exists(Path.Combine(Repository, "scripts", "ci", "Verify-Workflows.ps1")));
  }

  [Fact]
  public void InteropWorkflowRequiresRealGnuPgOnEveryHostedPlatform() {
    var yaml = File.ReadAllText(Path.Combine(Repository, ".github", "workflows", "interop.yml"));
    Assert.Contains("workflow_call:", yaml);
    Assert.Contains("source_sha:", yaml);
    Assert.Contains("required: true", yaml);
    Assert.Contains("os: [ubuntu-latest, windows-latest, macos-latest]", yaml);
    Assert.Contains("ASSUAN_GNUPG_REQUIRED: '1'", yaml);
    Assert.Contains("CALL_INPUTS:", yaml);
    Assert.Contains("-CallInputsJson", yaml);
    Assert.Contains("$call.Contains('source_sha')", yaml);
    Assert.Contains("--filter-namespace AssuanLibrary.Tests.Interop", yaml);
    Assert.Contains("ASSUAN_GPGCONF_PATH", yaml);
    Assert.Contains("ASSUAN_GPG_AGENT_PATH", yaml);
    Assert.Contains("ASSUAN_GPG_CONNECT_AGENT_PATH", yaml);
    Assert.Contains("scoop install main/gnupg", yaml);
    Assert.Contains("apps\\gnupg\\current\\bin", yaml);
    Assert.DoesNotContain("choco install", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("persist-credentials: false", yaml);
    Assert.Contains("if: always()", yaml);
    Assert.Contains("needs: [prepare, real]", yaml);
    Assert.Contains("Assert-Gate.ps1", yaml);
    Assert.DoesNotContain("secrets: inherit", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("contents: write", yaml, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("nuget push", yaml, StringComparison.OrdinalIgnoreCase);
    var actions = UsesRegex().Matches(yaml);
    Assert.NotEmpty(actions);
    Assert.All(actions, action => Assert.Matches("^[0-9a-f]{40}$", action.Groups["ref"].Value));
    Assert.Equal(3, CmdletRegex().Count(yaml));
  }

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*[^\s#]+@(?<ref>[^\s#]+)", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex UsesRegex();

  [GeneratedRegex("Assert-CheckoutSha\\.ps1", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex CmdletRegex();

  [GeneratedRegex(@"(?m)^\s*-\s*uses:\s*actions/checkout@", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex CheckoutRegex();
}
