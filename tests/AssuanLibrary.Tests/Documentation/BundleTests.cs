// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.Documentation;

public sealed class BundleTests {
  [Fact]
  public async Task VersionedBundleIsCuratedNonOverwritingAndDeterministic() {
    var root = ManifestTests.Repository;
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
    var token = TestContext.Current.CancellationToken;
    var full = await File.ReadAllTextAsync(Path.Combine(first, "llms-full.txt"), token);
    Assert.Contains("Getting started", full);
    Assert.DoesNotContain("Documentation operations", full);
    Assert.DoesNotContain("Continuous integration", full);
    Assert.DoesNotContain("Release operations", full);
    Assert.Contains("```csharp", full);
    var index = await File.ReadAllTextAsync(Path.Combine(first, "llms.txt"), token);
    Assert.Contains("/2.0.0/", index);
    Assert.Contains("Continuous integration", index);
    Assert.Contains("Release operations", index);
    Assert.Contains("/tree/v2.0.0/examples/AssuanLibrary.Examples", index);
    Assert.True(File.Exists(Path.Combine(root, "examples", "AssuanLibrary.Examples", "SExpressionExample.cs")));
    var packageReadme = await File.ReadAllTextAsync(Path.Combine(root, "docs", "nuget", "README.md"), token);
    foreach (Match match in Regex.Matches(packageReadme,
               @"https://baliestri\.github\.io/assuan-library/2\.0\.0/[^\s)]*",
               RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(5))) {
      var relative = match.Value["https://baliestri.github.io/assuan-library/2.0.0/".Length..];
      Assert.True(File.Exists(Path.Combine(first, "site", relative.Length == 0 ? "index.html" : relative)),
        "NuGet README site link missing: " + match.Value);
    }

    var before = await File.ReadAllBytesAsync(Path.Combine(first, "llms.txt"), token);
    await Assert.ThrowsAsync<IOException>(() => DocumentationBuilder.BuildAsync(root, VERSION,
      BASE_URL, firstOutput, TestContext.Current.CancellationToken));
    Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(first, "llms.txt"), token));
    var second = await DocumentationBuilder.BuildAsync(root, VERSION, BASE_URL, secondOutput,
      TestContext.Current.CancellationToken);
    var left = HashTree(first);
    var right = HashTree(second);
    Assert.Equal(left.Keys, right.Keys);
    foreach (var key in left.Keys)
      Assert.True(string.Equals(left[key], right[key], StringComparison.Ordinal), "Nondeterministic file: " + key);

    Assert.Empty(DocumentationBuilder.Verify(first));
  }

  [Theory]
  [InlineData("../outside.md")]
  [InlineData("C:/absolute.md")]
  public void ManifestRejectsEscapesAndAbsolutePaths(string invalid) {
    var repository = ManifestTests.Repository;
    var source = File.ReadAllText(ManifestTests.Manifest).Replace("docs/guides/getting-started.md", invalid);
    var filename = Path.Combine(repository, "artifacts", "docs-tests", Guid.NewGuid().ToString("N"), "sources.json");
    Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
    File.WriteAllText(filename, source);
    Assert.Throws<InvalidDataException>(() => DocumentationBuilder.ReadManifest(repository, filename));
  }

  [Fact]
  public void LocalLinkAndFragmentVerifierFindsMissingTargets() {
    var root = Path.Combine(ManifestTests.Repository, "artifacts", "docs-tests", Guid.NewGuid().ToString("N"));
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
