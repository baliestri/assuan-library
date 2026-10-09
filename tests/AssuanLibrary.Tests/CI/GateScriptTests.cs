// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text.Json;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.CI;

public sealed class GateScriptTests {
  private static readonly string _repository = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);
  private static readonly string _validSha = new('a', 40);

  [Theory]
  [InlineData("")]
  [InlineData("main")]
  [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
  [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
  public async Task ReusableCallRejectsAnythingButAFullLowercaseSha(string invalid) {
    var result = await RunPowerShellAsync("Resolve-SourceSha.ps1", "-EventName", "workflow_call",
      "-SourceSha", invalid);
    Assert.NotEqual(0, result.ExitCode);
    if (invalid.Length != 0) {
      Assert.DoesNotContain(invalid, result.Output);
    }
  }

  [Fact]
  public async Task ResolverSelectsOnlyTheShaForItsEvent() {
    var call = await RunPowerShellAsync("Resolve-SourceSha.ps1", "-EventName", "workflow_call",
      "-SourceSha", _validSha, "-EventSha", "not-a-sha");
    Assert.Equal(0, call.ExitCode);
    Assert.Equal(_validSha, call.Output.Trim());

    var direct = await RunPowerShellAsync("Resolve-SourceSha.ps1", "-EventName", "pull_request",
      "-EventSha", _validSha, "-SourceSha", "not-a-sha");
    Assert.Equal(0, direct.ExitCode);
    Assert.Equal(_validSha, direct.Output.Trim());
    Assert.NotEqual(0, (await RunPowerShellAsync("Resolve-SourceSha.ps1", "-EventName", "unknown",
      "-EventSha", _validSha)).ExitCode);
  }

  [Fact]
  public async Task CheckoutRequiresTheExactSelectedCommit() {
    var head = await RunAsync("git", "rev-parse", "HEAD");
    Assert.Equal(0, head.ExitCode);
    var selected = head.Output.Trim();
    Assert.Equal(0, (await RunPowerShellAsync("Assert-CheckoutSha.ps1", "-Sha", selected,
      "-Repository", _repository)).ExitCode);
    Assert.NotEqual(0, (await RunPowerShellAsync("Assert-CheckoutSha.ps1", "-Sha", _validSha,
      "-Repository", _repository)).ExitCode);
    Assert.NotEqual(0, (await RunPowerShellAsync("Assert-CheckoutSha.ps1", "-Sha", selected.ToUpperInvariant(),
      "-Repository", _repository)).ExitCode);
  }

  [Fact]
  public async Task GateRejectsMissingFailedCancelledAndSkippedRequiredJobs() {
    const string REQUIRED = "[\"matrix\",\"docs\"]";
    foreach (var state in new[] { "failure", "cancelled", "skipped" }) {
      var results = JsonSerializer.Serialize(new Dictionary<string, string> {
        ["matrix"] = "success", ["docs"] = state
      });
      var rejected = await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
        "-RequiredJobsJson", REQUIRED, "-ResultsJson", results);
      Assert.NotEqual(0, rejected.ExitCode);
      Assert.DoesNotContain(_validSha, rejected.Output);
    }

    Assert.NotEqual(0, (await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
      "-RequiredJobsJson", REQUIRED, "-ResultsJson", "{\"matrix\":\"success\"}")).ExitCode);
    Assert.NotEqual(0, (await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
      "-RequiredJobsJson", "[]", "-ResultsJson", "{}")).ExitCode);
  }

  [Theory]
  [InlineData("[\"prepare\",\"matrix\",\"docs\",\"format\",\"workflows\"]")]
  [InlineData("[\"prepare\",\"real\"]")]
  public async Task EveryRequiredJobBlocksTheGateUnlessSuccessful(string requiredJson) {
    var jobs = JsonSerializer.Deserialize<string[]>(requiredJson)!;
    foreach (var job in jobs) {
      foreach (var state in new[] { "failure", "cancelled", "skipped" }) {
        var results = jobs.ToDictionary(name => name, name => name == job ? state : "success");
        var rejected = await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
          "-RequiredJobsJson", requiredJson, "-ResultsJson", JsonSerializer.Serialize(results));
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.DoesNotContain(_validSha, rejected.Output);
      }

      var missing = jobs.Where(name => name != job).ToDictionary(name => name, _ => "success");
      Assert.NotEqual(0, (await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
        "-RequiredJobsJson", requiredJson, "-ResultsJson", JsonSerializer.Serialize(missing))).ExitCode);
    }
  }

  [Fact]
  public async Task FutureReleaseCanRejectGreenGatesFromDifferentCommits() {
    const string REQUIRED = "[\"ci\",\"interop\"]";
    const string SUCCEEDED = "{\"ci\":\"success\",\"interop\":\"success\"}";
    var matching = JsonSerializer.Serialize(new Dictionary<string, string> {
      ["ci"] = _validSha, ["interop"] = _validSha
    });
    var approved = await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
      "-RequiredJobsJson", REQUIRED, "-ResultsJson", SUCCEEDED, "-UpstreamShasJson", matching);
    Assert.Equal(0, approved.ExitCode);
    Assert.Equal(_validSha, approved.Output.Trim());

    var different = JsonSerializer.Serialize(new Dictionary<string, string> {
      ["ci"] = _validSha, ["interop"] = new string('b', 40)
    });
    foreach (var outputs in new[] { different, "{\"ci\":\"" + _validSha + "\"}" }) {
      var rejected = await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
        "-RequiredJobsJson", REQUIRED, "-ResultsJson", SUCCEEDED, "-UpstreamShasJson", outputs);
      Assert.NotEqual(0, rejected.ExitCode);
      Assert.DoesNotContain(_validSha, rejected.Output);
    }
  }

  [Fact]
  public async Task SuccessfulGatePublishesOnlyTheValidatedSha() {
    var parent = Path.Combine(_repository, "artifacts", "ci-script-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(parent);
    var outputFile = Path.Combine(parent, "github-output.txt");
    var success = await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
      "-RequiredJobsJson", "[\"matrix\",\"docs\"]",
      "-ResultsJson", "{\"matrix\":\"success\",\"docs\":\"success\"}",
      "-OutputFile", outputFile);
    Assert.Equal(0, success.ExitCode);
    Assert.Equal(_validSha, success.Output.Trim());
    Assert.Equal("source_sha=" + _validSha, File.ReadAllText(outputFile).Trim());

    var unwritable = await RunPowerShellAsync("Assert-Gate.ps1", "-Sha", _validSha,
      "-RequiredJobsJson", "[\"matrix\",\"docs\"]",
      "-ResultsJson", "{\"matrix\":\"success\",\"docs\":\"success\"}",
      "-OutputFile", Path.Combine(parent, "missing-parent", "github-output.txt"));
    Assert.NotEqual(0, unwritable.ExitCode);
    Assert.DoesNotContain(_validSha, unwritable.Output);
  }

  private static Task<ProcessResult> RunPowerShellAsync(string script, params string[] args)
    => RunAsync("pwsh", [
      "-NoProfile", "-NonInteractive", "-File",
      Path.Combine(_repository, "scripts", "ci", script), .. args
    ]);

  private static async Task<ProcessResult> RunAsync(string executable, params string[] args) {
    var start = new ProcessStartInfo(executable) {
      WorkingDirectory = _repository,
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    foreach (var argument in args) {
      start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
    var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken)
      .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
    return new ProcessResult(process.ExitCode, await output + await errors);
  }

  private sealed record ProcessResult(int ExitCode, string Output);
}
