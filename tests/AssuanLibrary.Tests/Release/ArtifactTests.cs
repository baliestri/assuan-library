// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AssuanLibrary.Tests.Release;

public sealed class ArtifactTests {
  [Fact]
  public async Task MatchingCommitAndPackageCanBeVerifiedOffline() {
    using var fixture = new ArtifactFixture();
    Assert.Equal(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Fact]
  public async Task PatchArtifactsUseTheSelectedVersionThroughout() {
    using var fixture = new ArtifactFixture("2.0.1");
    Assert.Equal(0, (await fixture.VerifyAsync()).ExitCode);
    Assert.Equal(0, (await fixture.BuildAsync("patch")).ExitCode);
    var token = TestContext.Current.CancellationToken;
    await File.WriteAllTextAsync(fixture.ManifestPath,
      (await File.ReadAllTextAsync(fixture.ManifestPath, token))
      .Replace("\"version\":\"2.0.1\"", "\"version\":\"2.0.0\"", StringComparison.Ordinal), token);
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Theory]
  [InlineData("WrongId")]
  [InlineData("WrongVersion")]
  [InlineData("MissingAnalyzer")]
  [InlineData("DuplicateAnalyzer")]
  [InlineData("MissingReadme")]
  [InlineData("MissingLicense")]
  [InlineData("DuplicateLicense")]
  [InlineData("MissingAuthor")]
  [InlineData("WrongRepository")]
  [InlineData("MissingDescription")]
  [InlineData("MandatoryExtensions")]
  public async Task RejectsInvalidNuGetPackageBeforeAnyPublication(string defect) {
    using var fixture = new ArtifactFixture();
    fixture.WritePackage(defect);
    fixture.RefreshHashes();
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Fact]
  public async Task RejectsPackageHashManifestGateAndCheckoutMismatches() {
    using var fixture = new ArtifactFixture();
    Assert.NotEqual(0, (await fixture.VerifyAsync(ciSha: new string('b', 40))).ExitCode);
    Assert.NotEqual(0, (await fixture.VerifyAsync(new string('b', 40),
      new string('b', 40), new string('b', 40))).ExitCode);
    var token = TestContext.Current.CancellationToken;
    var manifest = await File.ReadAllTextAsync(fixture.ManifestPath, token);
    await File.WriteAllTextAsync(fixture.ManifestPath,
      manifest.Replace(fixture.SourceSha, new string('b', 40), StringComparison.Ordinal), token);
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
    fixture.RefreshHashes();
    Assert.Equal(0, (await fixture.VerifyAsync()).ExitCode);
    // The manifest binds the exact bytes: a repacked but otherwise valid package no longer matches it.
    fixture.WritePackage();
    await using (var archive = await ZipFile.OpenAsync(fixture.PackagePath, ZipArchiveMode.Update, token)) {
      await using var extra = await archive.CreateEntry("extra.txt").OpenAsync(token);
      extra.Write("changed after binding"u8);
    }

    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Fact]
  public async Task ThePackageIsBoundToItsCommitOnceAndNeverRebound() {
    using var fixture = new ArtifactFixture();
    var first = await fixture.BuildAsync("first");
    var second = await fixture.BuildAsync("second");
    Assert.True(first.ExitCode == 0, first.Output);
    Assert.True(second.ExitCode == 0, second.Output);
    var token = TestContext.Current.CancellationToken;
    var left = await File.ReadAllTextAsync(Path.Combine(fixture.OutputRoot, "first", "release-artifacts.json"), token);
    var right = await File.ReadAllTextAsync(Path.Combine(fixture.OutputRoot, "second", "release-artifacts.json"),
      token);
    Assert.Equal(left, right);
    using var manifest = JsonDocument.Parse(left);
    Assert.Equal(fixture.SourceSha, manifest.RootElement.GetProperty("sourceSha").GetString());
    Assert.Equal("AssuanLibrary.2.0.0.nupkg", manifest.RootElement.GetProperty("packageName").GetString());
    Assert.False(manifest.RootElement.TryGetProperty("docsName", out var _));
    Assert.NotEqual(0, (await fixture.BuildAsync("first")).ExitCode);
  }

  [Fact]
  public async Task RegistrySigningMayAddEntriesButCannotChangeRequiredPackageBytes() {
    using var fixture = new ArtifactFixture();
    var registry = Path.Combine(fixture.OutputRoot, "registry", "AssuanLibrary.2.0.0.nupkg");
    Directory.CreateDirectory(Path.GetDirectoryName(registry)!);
    File.Copy(fixture.PackagePath, registry);
    var token = TestContext.Current.CancellationToken;
    await using (var archive = await ZipFile.OpenAsync(registry, ZipArchiveMode.Update, token)) {
      await using var signature = await archive.CreateEntry(".signature.p7s").OpenAsync(token);
      signature.Write("registry signature"u8);
    }

    Assert.Equal(0, (await fixture.CompareRegistryAsync(registry)).ExitCode);
    await using (var archive = await ZipFile.OpenAsync(registry, ZipArchiveMode.Update, token)) {
      archive.GetEntry("README.md")!.Delete();
      await using var readme = await archive.CreateEntry("README.md").OpenAsync(token);
      readme.Write("different content"u8);
    }

    Assert.NotEqual(0, (await fixture.CompareRegistryAsync(registry)).ExitCode);
  }

  private sealed class ArtifactFixture : IDisposable {
    private readonly ReleaseFixture _git = new();
    private readonly string _version;

    internal ArtifactFixture(string version = "2.0.0") {
      _version = version;
      var directory = Path.Combine(_git.Root, "artifacts", "candidate");
      Directory.CreateDirectory(directory);
      PackagePath = Path.Combine(directory, $"AssuanLibrary.{version}.nupkg");
      ManifestPath = Path.Combine(directory, "release-artifacts.json");
      WritePackage();
      RefreshHashes();
    }

    internal string PackagePath { get; }
    internal string ManifestPath { get; }
    internal string SourceSha => _git.Head;
    internal string OutputRoot => Path.Combine(_git.Root, "artifacts", "built");

    public void Dispose()
      => _git.Dispose();

    internal void WritePackage(string? defect = null) {
      File.Delete(PackagePath);
      using var archive = ZipFile.Open(PackagePath, ZipArchiveMode.Create);
      var id = string.Equals(defect, "WrongId", StringComparison.Ordinal) ? "DifferentLibrary" : "AssuanLibrary";
      var version = string.Equals(defect, "WrongVersion", StringComparison.Ordinal) ? "1.0.0" : _version;
      var dependency = string.Equals(defect, "MandatoryExtensions"
        , StringComparison.Ordinal)
        ? "<dependency id=\"Microsoft.Extensions.Logging.Abstractions\" version=\"10.0.0\" />"
        : "";
      var authors = string.Equals(defect, "MissingAuthor", StringComparison.Ordinal)
        ? ""
        : "<authors>Bruno Sales</authors>";
      var description = string.Equals(defect, "MissingDescription"
        , StringComparison.Ordinal)
        ? ""
        : "<description>Binary-safe asynchronous Assuan clients and servers.</description>";
      var repository = string.Equals(defect, "WrongRepository"
        , StringComparison.Ordinal)
        ? "https://example.invalid"
        : "https://github.com/baliestri/assuan-library";
      Add(archive, "AssuanLibrary.nuspec",
        $"<package><metadata><id>{id}</id><version>{version}</version>{authors}{description}<copyright>Copyright (c) 2026 Bruno Sales</copyright><projectUrl>https://github.com/baliestri/assuan-library</projectUrl><tags>assuan gpg gpgme gnupg</tags><readme>README.md</readme><license type=\"file\">LICENSE.md</license><repository type=\"git\" url=\"{repository}\" /><dependencies><group targetFramework=\"net10.0\">{dependency}</group></dependencies></metadata></package>");
      Add(archive, "lib/net10.0/AssuanLibrary.dll", "runtime");
      Add(archive, "lib/net10.0/AssuanLibrary.xml", "<doc/>");
      if (!string.Equals(defect, "MissingAnalyzer", StringComparison.Ordinal)) {
        Add(archive, "analyzers/dotnet/cs/AssuanLibrary.Generators.dll", "generator");
        if (string.Equals(defect, "DuplicateAnalyzer", StringComparison.Ordinal)) {
          Add(archive, "analyzers/dotnet/cs/AssuanLibrary.Generators.dll", "duplicate");
        }
      }

      if (!string.Equals(defect, "MissingReadme", StringComparison.Ordinal)) {
        Add(archive, "README.md", "# AssuanLibrary\n");
      }

      if (!string.Equals(defect, "MissingLicense", StringComparison.Ordinal)) {
        Add(archive, "LICENSE.md", "License text\n");
        if (string.Equals(defect, "DuplicateLicense", StringComparison.Ordinal)) {
          Add(archive, "LICENSE.md", "duplicate");
        }
      }
    }

    internal void RefreshHashes()
      => File.WriteAllText(ManifestPath, JsonSerializer.Serialize(new {
        version = _version,
        sourceSha = _git.Head,
        packageName = Path.GetFileName(PackagePath),
        packageSha256 = Hash(PackagePath)
      }));

    internal async Task<(int ExitCode, string Output)> VerifyAsync(string? preparedSha = null,
      string? ciSha = null, string? interopSha = null) {
      var start = new ProcessStartInfo("pwsh") {
        WorkingDirectory = ReleaseFixture.ToolsRoot, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true
      };
      foreach (var argument in new[] {
                 "-NoProfile", "-NonInteractive", "-File",
                 Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Verify-Release.ps1"),
                 "-Root", _git.Root, "-Version", _version, "-PreparedSha", preparedSha ?? _git.Head,
                 "-CiSha", ciSha ?? _git.Head, "-InteropSha", interopSha ?? _git.Head,
                 "-PackagePath", PackagePath, "-ManifestPath", ManifestPath
               })
        start.ArgumentList.Add(argument);

      using var process = Process.Start(start)!;
      var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
      var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
      await process.WaitForExitAsync(TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken).ConfigureAwait(false);
      return (process.ExitCode, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
    }

    internal async Task<(int ExitCode, string Output)> BuildAsync(string name) {
      var start = new ProcessStartInfo("pwsh") {
        WorkingDirectory = ReleaseFixture.ToolsRoot, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true
      };
      foreach (var argument in new[] {
                 "-NoProfile", "-NonInteractive", "-File",
                 Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Build-ReleaseArtifacts.ps1"),
                 "-Root", _git.Root, "-Version", _version, "-PreparedSha", _git.Head, "-PackagePath", PackagePath,
                 "-OutputDirectory", Path.Combine(OutputRoot, name)
               })
        start.ArgumentList.Add(argument);

      using var process = Process.Start(start)!;
      var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
      var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
      await process.WaitForExitAsync(TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken).ConfigureAwait(false);
      return (process.ExitCode, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
    }

    internal async Task<(int ExitCode, string Output)> CompareRegistryAsync(string registryPath) {
      var start = new ProcessStartInfo("pwsh") {
        WorkingDirectory = ReleaseFixture.ToolsRoot, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true
      };
      foreach (var argument in new[] {
                 "-NoProfile", "-NonInteractive", "-File",
                 Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Compare-RegistryPackage.ps1"),
                 "-OriginalPath", PackagePath, "-RegistryPath", registryPath, "-Version", _version
               })
        start.ArgumentList.Add(argument);

      using var process = Process.Start(start)!;
      var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
      var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
      await process.WaitForExitAsync(TestContext.Current.CancellationToken)
        .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken).ConfigureAwait(false);
      return (process.ExitCode, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
    }

    private static string Hash(string path)
      => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void Add(ZipArchive archive, string path, string content) {
      var entry = archive.CreateEntry(path);
      using var stream = entry.Open();
      var bytes = Encoding.UTF8.GetBytes(content);
      stream.Write(bytes);
    }
  }
}
