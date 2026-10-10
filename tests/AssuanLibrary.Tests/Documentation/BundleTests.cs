// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.Documentation;

public sealed class BundleTests {
  [Fact]
  public async Task VersionedBundleIsCuratedNonOverwritingAndDeterministic() {
    var root = ManifestTests._repository;
    var suffix = Guid.NewGuid().ToString("N");
    var firstOutput = Path.Combine(root, "artifacts", "docs-tests", suffix, "one");
    var secondOutput = Path.Combine(root, "artifacts", "docs-tests", suffix, "two");
    const string VERSION = "2.0.0";
    const string BASE_URL = "https://baliestri.github.io/assuan-library/";
    var first = await DocumentationBuilder.BuildAsync(root, VERSION, BASE_URL, firstOutput,
      TestContext.Current.CancellationToken);
    Assert.True(File.Exists(Path.Combine(first, "site", "api", "AssuanLibrary.Client.AssuanClient.html")));
    Assert.True(File.Exists(Path.Combine(first, "site", "api", "AssuanLibrary.Server.ServerSession-1.html")));
    Assert.False(File.Exists(Path.Combine(first, "site", "api", "AssuanLibrary.Transport.UnixNative.html")));
    var full = File.ReadAllText(Path.Combine(first, "llms-full.txt"));
    Assert.Contains("Getting started", full);
    Assert.DoesNotContain("Documentation operations", full);
    Assert.DoesNotContain("Continuous integration", full);
    Assert.DoesNotContain("Release operations", full);
    Assert.Contains("```csharp", full);
    Assert.Contains("/2.0.0/", File.ReadAllText(Path.Combine(first, "llms.txt")));
    Assert.Contains("Continuous integration", File.ReadAllText(Path.Combine(first, "llms.txt")));
    Assert.Contains("Release operations", File.ReadAllText(Path.Combine(first, "llms.txt")));
    Assert.Contains("/tree/v2.0.0/examples/AssuanLibrary.Examples",
      File.ReadAllText(Path.Combine(first, "llms.txt")));
    Assert.True(File.Exists(Path.Combine(root, "examples", "AssuanLibrary.Examples", "SExpressionExample.cs")));
    var packageReadme = File.ReadAllText(Path.Combine(root, "docs", "nuget", "README.md"));
    foreach (Match match in Regex.Matches(packageReadme,
               @"https://baliestri\.github\.io/assuan-library/2\.0\.0/[^\s)]*")) {
      var relative = match.Value["https://baliestri.github.io/assuan-library/2.0.0/".Length..];
      Assert.True(File.Exists(Path.Combine(first, "site", relative.Length == 0 ? "index.html" : relative)),
        "NuGet README site link missing: " + match.Value);
    }

    var before = File.ReadAllBytes(Path.Combine(first, "llms.txt"));
    await Assert.ThrowsAsync<IOException>(() => DocumentationBuilder.BuildAsync(root, VERSION,
      BASE_URL, firstOutput, TestContext.Current.CancellationToken));
    Assert.Equal(before, File.ReadAllBytes(Path.Combine(first, "llms.txt")));
    var second = await DocumentationBuilder.BuildAsync(root, VERSION, BASE_URL, secondOutput,
      TestContext.Current.CancellationToken);
    var left = HashTree(first);
    var right = HashTree(second);
    Assert.Equal(left.Keys, right.Keys);
    foreach (var key in left.Keys) {
      Assert.True(left[key] == right[key], "Nondeterministic file: " + key);
    }

    Assert.Empty(DocumentationBuilder.Verify(first));
  }

  [Theory]
  [InlineData("../outside.md")]
  [InlineData("C:/absolute.md")]
  public void ManifestRejectsEscapesAndAbsolutePaths(string invalid) {
    var repository = ManifestTests._repository;
    var source = File.ReadAllText(ManifestTests._manifest).Replace("docs/guides/getting-started.md", invalid);
    var filename = Path.Combine(repository, "artifacts", "docs-tests", Guid.NewGuid().ToString("N"), "sources.json");
    Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
    File.WriteAllText(filename, source);
    Assert.Throws<InvalidDataException>(() => DocumentationBuilder.ReadManifest(repository, filename));
  }

  [Fact]
  public void LocalLinkAndFragmentVerifierFindsMissingTargets() {
    var root = Path.Combine(ManifestTests._repository, "artifacts", "docs-tests", Guid.NewGuid().ToString("N"));
    var stage = Path.Combine(root, "_stage", "guides");
    var site = Path.Combine(root, "site", "guides");
    Directory.CreateDirectory(stage);
    Directory.CreateDirectory(site);
    File.WriteAllText(Path.Combine(stage, "page.md"), "# Page\n[bad](missing.md#section)\n");
    File.WriteAllText(Path.Combine(site, "page.html"), "<h1 id=\"page\">Page</h1>");
    Assert.Contains(DocumentationBuilder.Verify(root), item => item.Contains("Broken local link"));
    File.WriteAllText(Path.Combine(stage, "target.md"), "# Target\n## Section\n");
    File.WriteAllText(Path.Combine(site, "target.html"), "<h2 id=\"section\">Section</h2>");
    File.WriteAllText(Path.Combine(stage, "page.md"), "# Page\n[bad](target.md#absent)\n");
    Assert.Contains(DocumentationBuilder.Verify(root), item => item.Contains("Broken anchor"));
    File.WriteAllText(Path.Combine(stage, "page.md"), "# Page\n[good](target.md#section)\n");
    File.WriteAllText(Path.Combine(site, "page.html"), "<a href=\"target.html#missing\">bad</a>");
    Assert.Contains(DocumentationBuilder.Verify(root), item => item.Contains("Broken site anchor"));
    File.WriteAllText(Path.Combine(site, "page.html"), "<a href=\"absent.html\">bad</a>");
    Assert.Contains(DocumentationBuilder.Verify(root), item => item.Contains("Broken site link"));
  }

  private static SortedDictionary<string, string> HashTree(string root) {
    var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
      var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
      result.Add(relative, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }

    return result;
  }
}
