// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO.Compression;

namespace AssuanLibrary.Tests.Documentation;

public sealed class PackageReadmeTests {
  [Fact]
  public async Task MainPackageIncludesNuGetRootReadmeAndBundledAnalyzer() {
    var repository = ManifestTests.Repository;
    Assert.True(File.Exists(Path.Combine(repository, "docs", "nuget", "README.md")));
    var packages = Path.Combine(repository, "artifacts", "docs-package-tests");
    Directory.CreateDirectory(packages);
    var version = $"2.0.0-docs.{Guid.NewGuid():N}";
    var start = new ProcessStartInfo("dotnet") {
      WorkingDirectory = repository,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false
    };
    foreach (var value in new[] {
               "pack", "src/AssuanLibrary/AssuanLibrary.csproj", "-c", "Release",
               "--no-restore", "-o", packages, $"-p:PackageVersion={version}"
             })
      start.ArgumentList.Add(value);

    start.Environment["NUGET_PACKAGES"] = Path.Combine(repository, "artifacts", "nuget-cache");
    start.Environment["DOTNET_CLI_HOME"] = Path.Combine(repository, "artifacts", "dotnet-home");
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
    var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken)
      .WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
    Assert.True(process.ExitCode == 0, await stdout + await stderr);
    await using var archive = await ZipFile.OpenReadAsync(
      Path.Combine(packages, $"AssuanLibrary.{version}.nupkg"), TestContext.Current.CancellationToken);
    Assert.Single(archive.Entries, entry => string.Equals(entry.FullName, "README.md", StringComparison.Ordinal));
    Assert.Single(archive.Entries,
      entry => string.Equals(entry.FullName, "analyzers/dotnet/cs/AssuanLibrary.Generators.dll",
        StringComparison.Ordinal));
    var nuspec = Assert.Single(archive.Entries, entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
    using var reader = new StreamReader(await nuspec.OpenAsync(TestContext.Current.CancellationToken));
    Assert.Contains("<readme>README.md</readme>", await reader.ReadToEndAsync(TestContext.Current.CancellationToken),
      StringComparison.Ordinal);
    using var readme = new StreamReader(await Assert.Single(archive.Entries,
        entry => string.Equals(entry.FullName, "README.md", StringComparison.Ordinal))
      .OpenAsync(TestContext.Current.CancellationToken));
    var markdown = await readme.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Contains("net10.0", markdown, StringComparison.Ordinal);
    Assert.Equal(
      await File.ReadAllTextAsync(Path.Combine(repository, "docs", "nuget", "README.md"),
        TestContext.Current.CancellationToken), markdown);
    Assert.DoesNotContain("../", markdown, StringComparison.Ordinal);
    Assert.Contains("https://", markdown, StringComparison.Ordinal);
  }
}
