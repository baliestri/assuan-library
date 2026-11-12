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
  public async Task MatchingCommitPackageAndDocumentationCanBeVerifiedOffline() {
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

  [Theory]
  [InlineData("../escape.txt")]
  [InlineData("2.0.0/../escape.txt")]
  [InlineData("/absolute.txt")]
  public async Task RejectsDocumentationArchivePathsOutsideTheVersion(string unsafePath) {
    using var fixture = new ArtifactFixture();
    fixture.WriteDocs(unsafePath);
    fixture.RefreshHashes();
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Fact]
  public async Task RejectsSymbolicLinksInsideTheDocumentationArchive() {
    using var fixture = new ArtifactFixture();
    fixture.WriteDocs("2.0.0/link", true);
    fixture.RefreshHashes();
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Fact]
  public async Task RejectsDocumentationWithoutTheFullLlmGuide() {
    using var fixture = new ArtifactFixture();
    fixture.WriteDocs(missingPath: "2.0.0/llms-full.txt");
    fixture.RefreshHashes();
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Fact]
  public async Task RejectsChecksumManifestGateAndCheckoutMismatches() {
    using var fixture = new ArtifactFixture();
    Assert.NotEqual(0, (await fixture.VerifyAsync(ciSha: new string('b', 40))).ExitCode);
    Assert.NotEqual(0, (await fixture.VerifyAsync(new string('b', 40),
      new string('b', 40), new string('b', 40))).ExitCode);
    var token = TestContext.Current.CancellationToken;
    await File.WriteAllTextAsync(fixture.ChecksumPath, $"{new string('0', 64)}  docs-v2.0.0.zip\n", token);
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
    fixture.RefreshHashes();
    await File.WriteAllTextAsync(fixture.ManifestPath,
      (await File.ReadAllTextAsync(fixture.ManifestPath, token)).Replace(fixture.SourceSha, new string('b', 40),
        StringComparison.Ordinal),
      token);
    Assert.NotEqual(0, (await fixture.VerifyAsync()).ExitCode);
  }

  [Fact]
  public async Task TwoEquivalentBundlesProduceTheSameImmutableArchive() {
    using var fixture = new ArtifactFixture();
    var first = await fixture.BuildAsync("first");
    var second = await fixture.BuildAsync("second");
    Assert.Equal(0, first.ExitCode);
    Assert.Equal(0, second.ExitCode);
    var left = Path.Combine(fixture.OutputRoot, "first", "docs-v2.0.0.zip");
    var right = Path.Combine(fixture.OutputRoot, "second", "docs-v2.0.0.zip");
    var token = TestContext.Current.CancellationToken;
    Assert.Equal(await File.ReadAllBytesAsync(left, token), await File.ReadAllBytesAsync(right, token));
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
      DocsPath = Path.Combine(directory, $"docs-v{version}.zip");
      ChecksumPath = Path.Combine(directory, $"docs-v{version}.sha256");
      ManifestPath = Path.Combine(directory, "release-artifacts.json");
      WritePackage();
      WriteDocs();
      RefreshHashes();
    }

    internal string PackagePath { get; }
    private string DocsPath { get; }
    internal string ChecksumPath { get; }
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
        Add(archive, "LICENSE.md", "MIT License\n");
        if (string.Equals(defect, "DuplicateLicense", StringComparison.Ordinal)) {
          Add(archive, "LICENSE.md", "duplicate");
        }
      }
    }

    internal void WriteDocs(string? additionalPath = null, bool symlink = false, string? missingPath = null) {
      File.Delete(DocsPath);
      using var archive = ZipFile.Open(DocsPath, ZipArchiveMode.Create);
      foreach (var path in new[] {
                 $"{_version}/site/index.html", $"{_version}/site/api/toc.html",
                 $"{_version}/llms.txt", $"{_version}/llms-full.txt", $"{_version}/metadata.json"
               })
        if (!string.Equals(path, missingPath, StringComparison.Ordinal)) {
          Add(archive, path, path);
        }

      if (additionalPath is not null) {
        var added = Add(archive, additionalPath, "unexpected");
        if (symlink) {
          added.ExternalAttributes = unchecked((int)0xA0000000);
        }
      }
    }

    internal void RefreshHashes() {
      var docsHash = Hash(DocsPath);
      File.WriteAllText(ChecksumPath, $"{docsHash}  docs-v{_version}.zip\n");
      File.WriteAllText(ManifestPath, JsonSerializer.Serialize(new {
        version = _version,
        sourceSha = _git.Head,
        packageName = Path.GetFileName(PackagePath),
        packageSha256 = Hash(PackagePath),
        docsName = Path.GetFileName(DocsPath),
        docsSha256 = docsHash
      }));
    }

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
                 "-PackagePath", PackagePath, "-DocsArchive", DocsPath,
                 "-ChecksumPath", ChecksumPath, "-ManifestPath", ManifestPath
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
      var bundle = Path.Combine(_git.Root, "artifacts", "bundles", name, _version);
      foreach (var path in new[] {
                 "site/index.html", "site/api/toc.html", "llms.txt",
                 "llms-full.txt", "metadata.json"
               }) {
        var filename = Path.Combine(bundle, path);
        Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
        await File.WriteAllTextAsync(filename, path, TestContext.Current.CancellationToken).ConfigureAwait(false);
      }

      var start = new ProcessStartInfo("pwsh") {
        WorkingDirectory = ReleaseFixture.ToolsRoot, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true
      };
      foreach (var argument in new[] {
                 "-NoProfile", "-NonInteractive", "-File",
                 Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Build-ReleaseArtifacts.ps1"),
                 "-Root", _git.Root, "-Version", _version, "-PreparedSha", _git.Head, "-PackagePath", PackagePath,
                 "-BundlePath", bundle, "-OutputDirectory", Path.Combine(OutputRoot, name)
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

    private static ZipArchiveEntry Add(ZipArchive archive, string path, string content) {
      var entry = archive.CreateEntry(path);
      using var stream = entry.Open();
      var bytes = Encoding.UTF8.GetBytes(content);
      stream.Write(bytes);
      return entry;
    }
  }
}
