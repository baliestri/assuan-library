// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;

namespace AssuanLibrary.Tests.Release;

public sealed class VersionTests {
  [Theory]
  [InlineData("v2.0.0")]
  [InlineData("2.0.0-rc.1")]
  [InlineData("2.0")]
  [InlineData("2.0.0+build")]
  public async Task RejectsAnythingButTheStableFirstCandidate(string version) {
    using var fixture = new ReleaseFixture();
    var result = await fixture.PrepareAsync(version);
    Assert.NotEqual(0, result.ExitCode);
    Assert.Equal(ReleaseFixture.HistoricalChangelog, File.ReadAllText(Path.Combine(fixture.Root, "CHANGELOG.md")));
  }

  [Fact]
  public async Task RejectsDirtyCheckoutMovedHeadAndExistingReleaseRefs() {
    using var fixture = new ReleaseFixture();
    File.AppendAllText(Path.Combine(fixture.Root, "feature.txt"), "dirty\n");
    Assert.NotEqual(0, (await fixture.PrepareAsync()).ExitCode);
    File.WriteAllText(Path.Combine(fixture.Root, "feature.txt"), "Version two.\n");
    Assert.NotEqual(0, (await fixture.PrepareAsync(extra: ["-ExpectedSha", new string('a', 40)])).ExitCode);
    fixture.Git("tag", "v2.0.0");
    Assert.NotEqual(0, (await fixture.PrepareAsync()).ExitCode);
  }

  [Fact]
  public async Task RejectsMainThatIsNotAnAncestorOfDevelop() {
    using var fixture = new ReleaseFixture();
    fixture.Git("switch", "main");
    File.WriteAllText(Path.Combine(fixture.Root, "main-only.txt"), "diverged\n");
    fixture.Git("add", "main-only.txt");
    fixture.Git("commit", "-m", "fix: change published branch");
    fixture.Git("push", "origin", "main"); // Only the disposable bare fixture under artifacts.
    fixture.Git("switch", "develop");
    Assert.NotEqual(0, (await fixture.PrepareAsync()).ExitCode);
  }

  [Fact]
  public async Task RejectsAReleaseBranchThatAlreadyExistsOnlyOnOrigin() {
    using var fixture = new ReleaseFixture();
    fixture.Git("push", "origin", "HEAD:refs/heads/release/v2.0.0"); // Disposable bare fixture only.
    Assert.NotEqual(0, (await fixture.PrepareAsync()).ExitCode);
  }

  [Fact]
  public async Task DryRunReportsVersionEditsWithoutChangingTrackedFiles() {
    using var fixture = new ReleaseFixture();
    var project = Path.Combine(fixture.Root, "src", "AssuanLibrary", "AssuanLibrary.csproj");
    var before = File.ReadAllText(project);
    var result = await fixture.PrepareAsync(extra: ["-ExpectedSha", fixture.Head]);
    Assert.Equal(0, result.ExitCode);
    using var proposal = JsonDocument.Parse(result.Output);
    Assert.Equal(fixture.Head, proposal.RootElement.GetProperty("sourceSha").GetString());
    Assert.Contains(proposal.RootElement.GetProperty("files").EnumerateArray(), item =>
      item.GetString() == "src/AssuanLibrary/AssuanLibrary.csproj");
    Assert.Equal(before, File.ReadAllText(project));
    Assert.Equal(ReleaseFixture.HistoricalChangelog, File.ReadAllText(Path.Combine(fixture.Root, "CHANGELOG.md")));
  }

  [Fact]
  public async Task InsertsTheVersionWhenTheMainPackageHasNoVersionProperty() {
    using var fixture = new ReleaseFixture(versionProperty: false);
    Assert.Equal(0, (await fixture.PrepareAsync(dryRun: false)).ExitCode);
    var project = File.ReadAllText(Path.Combine(fixture.Root, "src", "AssuanLibrary", "AssuanLibrary.csproj"));
    Assert.Contains("<Version>2.0.0</Version>", project);
  }

  [Fact]
  public async Task ResumeRequiresTheExactExistingPreparedBranchWithoutReplacingIt() {
    using var fixture = new ReleaseFixture();
    Assert.Equal(0, (await fixture.PrepareAsync(dryRun: false)).ExitCode);
    fixture.Git("switch", "-c", "release/v2.0.0");
    fixture.Git("add", "CHANGELOG.md", "README.adoc", "docs/guides/getting-started.md",
      "src/AssuanLibrary/AssuanLibrary.csproj");
    fixture.Git("commit", "-m", "chore(release): prepare for 2.0.0");
    fixture.Git("push", "origin", "HEAD:refs/heads/release/v2.0.0"); // Disposable bare fixture only.
    var prepared = fixture.Head;
    Assert.Equal(0, (await fixture.ResumeAsync(prepared)).ExitCode);
    Assert.NotEqual(0, (await fixture.ResumeAsync(new string('a', 40))).ExitCode);
    Assert.Equal(prepared, fixture.Head);

    fixture.Git("switch", "-c", "advance");
    File.WriteAllText(Path.Combine(fixture.Root, "reviewed-fix.txt"), "new candidate\n");
    fixture.Git("add", "reviewed-fix.txt");
    fixture.Git("commit", "-m", "fix(ci): correct reusable SHA selection");
    fixture.Git("push", "origin", "HEAD:refs/heads/release/v2.0.0"); // Disposable bare fixture only.
    fixture.Git("switch", "release/v2.0.0");
    Assert.NotEqual(0, (await fixture.ResumeAsync(prepared)).ExitCode);
  }

  [Fact]
  public async Task ResumeRejectsAnUnpreparedBranchEvenWhenItsShaMatches() {
    using var fixture = new ReleaseFixture();
    fixture.Git("switch", "-c", "release/v2.0.0");
    fixture.Git("push", "origin", "HEAD:refs/heads/release/v2.0.0"); // Disposable bare fixture only.
    Assert.NotEqual(0, (await fixture.ResumeAsync(fixture.Head)).ExitCode);
  }

  [Fact]
  public async Task PatchCandidateMustBeStableAndNewerThanPublishedTag() {
    using var fixture = new ReleaseFixture(patchRelease: true);
    var preview = await fixture.PrepareAsync("2.0.1");
    Assert.True(preview.ExitCode == 0, preview.Output);
    foreach (var version in new[] { "2.0.0", "2.0.1-rc.1", "2.0.1+build", "3.0.0" }) {
      Assert.NotEqual(0, (await fixture.PrepareAsync(version)).ExitCode);
    }
  }

  [Fact]
  public async Task PatchCandidateRejectsAChangedLocalPublishedTag() {
    using var fixture = new ReleaseFixture(patchRelease: true);
    fixture.Git("tag", "-f", "v2.0.0", fixture.Head);
    Assert.NotEqual(0, (await fixture.PrepareAsync("2.0.1")).ExitCode);
  }

  [Fact]
  public async Task PatchPreparationUpdatesPackageReadmeAndLeavesDryRunUntouched() {
    using var fixture = new ReleaseFixture(patchRelease: true);
    var path = Path.Combine(fixture.Root, "docs", "nuget", "README.md");
    var before = File.ReadAllText(path);
    var preview = await fixture.PrepareAsync("2.0.1");
    Assert.True(preview.ExitCode == 0, preview.Output);
    Assert.Equal(before, File.ReadAllText(path));
    using var proposal = JsonDocument.Parse(preview.Output);
    Assert.Contains(proposal.RootElement.GetProperty("files").EnumerateArray(), item =>
      item.GetString() == "docs/nuget/README.md");
    Assert.Equal(0, (await fixture.PrepareAsync("2.0.1", dryRun: false)).ExitCode);
    var prepared = File.ReadAllText(path);
    Assert.Contains("dotnet add package AssuanLibrary --version 2.0.1", prepared);
    Assert.Contains("/assuan-library/2.0.1/guides/", prepared);
  }

  [Fact]
  public void FixtureUsesThePublishedReadmeEvenWhenTheSourceIsPreparedForAPatch() {
    const string prepared = "dotnet add package AssuanLibrary --version 2.0.1\n" +
                            "https://baliestri.github.io/assuan-library/2.0.1/guides/getting-started.html\n" +
                            "https://github.com/baliestri/assuan-library/tree/v2.0.1/examples/AssuanLibrary.Examples\n";
    var baseline = ReleaseFixture.BaselinePackageReadme(prepared);
    Assert.Contains("dotnet add package AssuanLibrary --version 2.0.0", baseline);
    Assert.Contains("/assuan-library/2.0.0/guides/", baseline);
    Assert.Contains("/tree/v2.0.0/examples/", baseline);
    Assert.DoesNotContain("2.0.1", baseline);
  }

  [Fact]
  public async Task PatchResumeRequiresMatchingPreparedBranch() {
    using var fixture = new ReleaseFixture(patchRelease: true);
    Assert.Equal(0, (await fixture.PrepareAsync("2.0.1", dryRun: false)).ExitCode);
    fixture.Git("switch", "-c", "release/v2.0.1");
    fixture.Git("add", "CHANGELOG.md", "README.adoc", "docs/guides/getting-started.md",
      "src/AssuanLibrary/AssuanLibrary.csproj", "docs/nuget/README.md");
    fixture.Git("commit", "-m", "chore(release): prepare for 2.0.1");
    fixture.Git("push", "origin", "HEAD:refs/heads/release/v2.0.1");
    var sha = fixture.Head;
    Assert.Equal(0, (await fixture.ResumeAsync(sha, "2.0.1")).ExitCode);
    Assert.NotEqual(0, (await fixture.ResumeAsync(new string('a', 40), "2.0.1")).ExitCode);
  }
}
