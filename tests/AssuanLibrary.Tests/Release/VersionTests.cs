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
}
