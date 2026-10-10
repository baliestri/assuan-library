// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AssuanLibrary.Tests.Release;

public sealed class PagesTests {
  [Fact]
  public async Task StableBundlesAreAssembledOnceAndTheGreatestSemVerIsTheRoot() {
    using var fixture = new PagesFixture();
    fixture.Add("v2.1.0");
    fixture.Add("v1.2.0", createAssets: false);
    fixture.Add("v2.0.0");
    fixture.Add("v3.0.0", true, createAssets: false);
    fixture.Add("v3.1.0-rc.1", prerelease: true, createAssets: false);
    var assembled = await fixture.AssembleAsync();
    Assert.True(assembled.ExitCode == 0, assembled.Output);
    Assert.True(File.Exists(Path.Combine(fixture.Site, "2.0.0", "index.html")));
    Assert.True(File.Exists(Path.Combine(fixture.Site, "2.1.0", "llms.txt")));
    Assert.Contains("2.1.0", File.ReadAllText(Path.Combine(fixture.Site, "index.html")));
    Assert.Equal(File.ReadAllText(Path.Combine(fixture.Site, "2.1.0", "llms.txt")),
      File.ReadAllText(Path.Combine(fixture.Site, "llms.txt")));
    Assert.False(Directory.Exists(Path.Combine(fixture.Site, "1.2.0")));
    Assert.False(Directory.Exists(Path.Combine(fixture.Site, "3.0.0")));
    Assert.NotEqual(0, (await fixture.AssembleAsync()).ExitCode);
  }

  [Theory]
  [InlineData("MissingArchive")]
  [InlineData("WrongChecksum")]
  [InlineData("UnsafePath")]
  [InlineData("DuplicateVersion")]
  public async Task InvalidHistoricalReleaseCannotPromoteAPartialSite(string defect) {
    using var fixture = new PagesFixture();
    fixture.Add("v2.0.0");
    switch (defect) {
      case "MissingArchive":
        fixture.Add("v2.1.0", createAssets: false);
        break;
      case "WrongChecksum":
        fixture.Add("v2.1.0", corruptChecksum: true);
        break;
      case "UnsafePath":
        fixture.Add("v2.1.0", unsafePath: "2.1.0/../escape.txt");
        break;
      case "DuplicateVersion":
        fixture.Add("v2.0.0");
        break;
    }

    Assert.NotEqual(0, (await fixture.AssembleAsync()).ExitCode);
    Assert.False(Directory.Exists(fixture.Site));
  }

  [Fact]
  public async Task NumericSemVerOrderingWinsOverUploadOrderAndLexicalOrder() {
    using var fixture = new PagesFixture();
    fixture.Add("v2.10.0");
    fixture.Add("v2.9.0");
    var assembled = await fixture.AssembleAsync();
    Assert.True(assembled.ExitCode == 0, assembled.Output);
    Assert.Contains("2.10.0", File.ReadAllText(Path.Combine(fixture.Site, "index.html")));
    Assert.Equal(File.ReadAllText(Path.Combine(fixture.Site, "2.10.0", "llms.txt")),
      File.ReadAllText(Path.Combine(fixture.Site, "llms.txt")));
  }

  [Fact]
  public async Task AFailedRerunDoesNotReplaceAnExistingSite() {
    using var fixture = new PagesFixture();
    fixture.Add("v2.0.0");
    var assembled = await fixture.AssembleAsync();
    Assert.True(assembled.ExitCode == 0, assembled.Output);
    var original = File.ReadAllBytes(Path.Combine(fixture.Site, "index.html"));
    fixture.Add("v2.1.0", corruptChecksum: true);
    Assert.NotEqual(0, (await fixture.AssembleAsync()).ExitCode);
    Assert.Equal(original, File.ReadAllBytes(Path.Combine(fixture.Site, "index.html")));
  }

  private sealed class PagesFixture : IDisposable {
    private readonly List<ReleaseEntry> _releases = [];

    private readonly string _root = Path.Combine(ReleaseFixture.ToolsRoot, "artifacts", "pages-tests",
      Guid.NewGuid().ToString("N"));

    internal PagesFixture() => Directory.CreateDirectory(_root);
    internal string Site => Path.Combine(_root, "site");

    public void Dispose() => Directory.Delete(_root, true);

    internal void Add(string tag, bool draft = false, bool prerelease = false, bool createAssets = true,
    bool corruptChecksum = false, string? unsafePath = null) {
      var version = tag.TrimStart('v');
      var assets = Path.Combine(_root, "assets", version, _releases.Count.ToString());
      var archivePath = Path.Combine(assets, $"docs-v{version}.zip");
      var checksumPath = Path.Combine(assets, $"docs-v{version}.sha256");
      if (createAssets) {
        Directory.CreateDirectory(assets);
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) {
          foreach (var path in new[] {
                     $"{version}/site/index.html", $"{version}/site/api/toc.html",
                     $"{version}/site/llms.txt", $"{version}/site/llms-full.txt",
                     $"{version}/llms.txt", $"{version}/llms-full.txt", $"{version}/metadata.json"
                   }) {
            var entry = archive.CreateEntry(path);
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes($"Documentation for {version}: {path}"));
          }

          if (unsafePath is not null) {
            using var stream = archive.CreateEntry(unsafePath).Open();
            stream.Write("unsafe"u8);
          }
        }

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath))).ToLowerInvariant();
        File.WriteAllText(checksumPath, (corruptChecksum ? new string('0', 64) : hash) +
                                        $"  docs-v{version}.zip\n");
      }

      _releases.Add(new ReleaseEntry(tag, draft, prerelease, archivePath, checksumPath));
    }

    internal async Task<(int ExitCode, string Output)> AssembleAsync() {
      var manifest = Path.Combine(_root, "releases.json");
      File.WriteAllText(manifest, JsonSerializer.Serialize(_releases,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
      var start = new ProcessStartInfo("pwsh") {
        WorkingDirectory = ReleaseFixture.ToolsRoot, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true
      };
      foreach (var argument in new[] {
                 "-NoProfile", "-NonInteractive", "-File",
                 Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Assemble-Pages.ps1"),
                 "-Root", ReleaseFixture.ToolsRoot, "-ManifestPath", manifest, "-OutputDirectory", Site
               }) {
        start.ArgumentList.Add(argument);
      }

      using var process = Process.Start(start)!;
      var stdout = process.StandardOutput.ReadToEndAsync();
      var stderr = process.StandardError.ReadToEndAsync();
      await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
      return (process.ExitCode, await stdout + await stderr);
    }

    private sealed record ReleaseEntry(string TagName,
      bool Draft,
      bool Prerelease,
      string Archive,
      string Checksum);
  }
}
