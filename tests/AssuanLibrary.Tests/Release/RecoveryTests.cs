// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text.Json;

namespace AssuanLibrary.Tests.Release;

public sealed class RecoveryTests {
  [Theory]
  [InlineData("BranchExists", "Preflight")]
  [InlineData("GateSkipped", "Prepared")]
  [InlineData("GateShaMismatch", "Prepared")]
  [InlineData("MainMoved", "Built")]
  [InlineData("TagConflict", "Built")]
  [InlineData("NuGetTimeoutAfterUpload", "PublishingNuGet")]
  [InlineData("ReleaseAssetMismatch", "PublishedNuGet")]
  [InlineData("PagesBundleMissing", "GithubRelease")]
  [InlineData("DevelopMoved", "Pages")]
  public async Task UnsafeTransitionsStopAtTheirPhaseWithoutRetryOrForce(string defect, string expectedPhase) {
    var scenario = Baseline();
    switch (defect) {
      case "BranchExists":
        scenario["branchExists"] = true;
        break;
      case "GateSkipped":
        scenario["ciResult"] = "skipped";
        break;
      case "GateShaMismatch":
        scenario["interopSha"] = new string('b', 40);
        break;
      case "MainMoved":
        scenario["mainStillAncestor"] = false;
        break;
      case "TagConflict":
        scenario["tagConflict"] = true;
        break;
      case "NuGetTimeoutAfterUpload":
        scenario["uploadResult"] = "timeout";
        scenario["registryContainsVersion"] = true;
        break;
      case "ReleaseAssetMismatch":
        scenario["githubAssetsMatch"] = false;
        break;
      case "PagesBundleMissing":
        scenario["pagesBundlePresent"] = false;
        break;
      case "DevelopMoved":
        scenario["developCanFastForward"] = false;
        break;
    }

    var result = await RunAsync(scenario);
    Assert.Equal(0, result.ExitCode);
    using var json = JsonDocument.Parse(result.Output);
    Assert.Equal(expectedPhase, json.RootElement.GetProperty("lastPhase").GetString());
    Assert.True(json.RootElement.GetProperty("requiresManualInspection").GetBoolean());
    Assert.False(json.RootElement.GetProperty("attemptedSecondUpload").GetBoolean());
    Assert.False(json.RootElement.GetProperty("attemptedForce").GetBoolean());
    Assert.DoesNotContain("SECRET_MARKER", result.Output);
  }

  [Fact]
  public async Task ValidMockedResponsesCompleteWithoutRemoteSideEffects() {
    var result = await RunAsync(Baseline());
    Assert.Equal(0, result.ExitCode);
    using var json = JsonDocument.Parse(result.Output);
    Assert.Equal("Complete", json.RootElement.GetProperty("lastPhase").GetString());
    Assert.False(json.RootElement.GetProperty("requiresManualInspection").GetBoolean());
  }

  private static Dictionary<string, object> Baseline() => new() {
    ["preparedSha"] = new string('a', 40), ["checkedOutSha"] = new string('a', 40),
    ["ciSha"] = new string('a', 40), ["interopSha"] = new string('a', 40),
    ["ciResult"] = "success", ["interopResult"] = "success",
    ["branchExists"] = false, ["mainStillAncestor"] = true, ["tagConflict"] = false,
    ["artifactsValid"] = true, ["uploadResult"] = "success", ["registryContainsVersion"] = true,
    ["githubAssetsMatch"] = true, ["pagesBundlePresent"] = true, ["developCanFastForward"] = true,
    ["ignoredToken"] = "SECRET_MARKER"
  };

  private static async Task<(int ExitCode, string Output)> RunAsync(Dictionary<string, object> scenario) {
    var start = new ProcessStartInfo("pwsh") {
      WorkingDirectory = ReleaseFixture.ToolsRoot, UseShellExecute = false,
      RedirectStandardOutput = true, RedirectStandardError = true
    };
    foreach (var argument in new[] {
               "-NoProfile", "-NonInteractive", "-File",
               Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Simulate-Release.ps1"),
               "-InputJson", JsonSerializer.Serialize(scenario)
             }) {
      start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    return (process.ExitCode, await stdout + await stderr);
  }
}
