// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;

namespace AssuanLibrary.Tests.Release;

public sealed class ChangelogTests {
  [Fact]
  public void ReleaseToolManifestPinsEveryHostedArchiveByDigest() {
    var manifest = Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "tools.json");
    using var document = JsonDocument.Parse(File.ReadAllText(manifest));
    foreach (var (tool, version) in new[] { ("gitCliff", "2.14.2"), ("typos", "1.50.3") }) {
      var entry = document.RootElement.GetProperty(tool);
      Assert.Equal(version, entry.GetProperty("version").GetString());
      foreach (var platform in new[] { "win-x64", "linux-x64", "osx-arm64", "osx-x64" }) {
        var archive = entry.GetProperty("archives").GetProperty(platform);
        Assert.Contains(version, archive.GetProperty("name").GetString());
        Assert.Matches("^[0-9a-f]{64}$", archive.GetProperty("sha256").GetString()!);
      }
    }
  }

  [Fact]
  public async Task GeneratedSectionMarksBreakingChangesAndKeepsTheHistoricalTail() {
    using var fixture = new ReleaseFixture();
    var first = await fixture.PrepareAsync();
    Assert.Equal(0, first.ExitCode);
    using var preview = JsonDocument.Parse(first.Output);
    var section = preview.RootElement.GetProperty("generatedSection").GetString()!;
    Assert.Contains("## [2.0.0]", section);
    Assert.Contains("[**breaking**]", section);
    Assert.Contains("v1.2.0..v2.0.0", section);
    var repeated = await fixture.PrepareAsync();
    Assert.Equal(first.Output, repeated.Output);

    var applied = await fixture.PrepareAsync(dryRun: false);
    Assert.Equal(0, applied.ExitCode);
    var changelog = File.ReadAllText(Path.Combine(fixture.Root, "CHANGELOG.md"));
    Assert.EndsWith(ReleaseFixture.HistoricalChangelog["# Assuan Library .NET Changelog\n\n".Length..], changelog);
    Assert.Equal(1, changelog.Split("## [2.0.0]", StringSplitOptions.None).Length - 1);
    Assert.NotEqual(0, (await fixture.PrepareAsync(dryRun: false)).ExitCode);
  }

  [Fact]
  public async Task PatchChangelogStartsAtThePublishedTag() {
    using var fixture = new ReleaseFixture(patchRelease: true);
    var preview = await fixture.PrepareAsync("2.0.1");
    Assert.Equal(0, preview.ExitCode);
    using var proposal = JsonDocument.Parse(preview.Output);
    var section = proposal.RootElement.GetProperty("generatedSection").GetString()!;
    Assert.Contains("v2.0.0..v2.0.1", section);
    Assert.DoesNotContain("replace the published 1.x API", section);
    Assert.Equal(0, (await fixture.PrepareAsync("2.0.1", dryRun: false)).ExitCode);
    Assert.Contains("## [2.0.0] - published", File.ReadAllText(Path.Combine(fixture.Root, "CHANGELOG.md")));
  }
}
