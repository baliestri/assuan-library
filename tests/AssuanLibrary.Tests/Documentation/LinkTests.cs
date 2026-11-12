// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using System.Text.RegularExpressions;

namespace AssuanLibrary.Tests.Documentation;

public sealed partial class LinkTests {
  private readonly static string Root = RepositoryRoot.Path;

  [GeneratedRegex(@"(?<!!)\[[^\]]+\]\((?<target>[^)\s]+)\)", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex LinkRegex { get; }

  [GeneratedRegex(@"(?m)^#{1,6}[ \t]+(?<title>.+?)[ \t]*$", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex HeadingRegex { get; }

  [GeneratedRegex("(?ms)^```.*?^```[ \t]*$", RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex FenceRegex { get; }

  public static TheoryData<string> Pages() {
    var pages = new TheoryData<string> { "README.md", "CONTRIBUTING.md", "SECURITY.md" };
    foreach (var page in Directory.EnumerateFiles(Path.Combine(Root, "wiki"), "*.md").Order(StringComparer.Ordinal))
      pages.Add($"wiki/{Path.GetFileName(page)}");

    return pages;
  }

  [Theory]
  [MemberData(nameof(Pages))]
  public void EveryRelativeLinkAndAnchorResolves(string page) {
    var source = Path.Combine(Root, page.Replace('/', Path.DirectorySeparatorChar));
    var text = WithoutCode(File.ReadAllText(source));
    foreach (Match match in LinkRegex.Matches(text)) {
      var target = match.Groups["target"].Value;
      if (Uri.TryCreate(target, UriKind.Absolute, out var absolute)) {
        Assert.True(absolute.Scheme is "https", $"{page}: unsupported link scheme in {target}");
        Assert.DoesNotContain("baliestri.github.io", absolute.Host, StringComparison.OrdinalIgnoreCase);
        continue;
      }

      var parts = target.Split('#', 2);
      var linked = parts[0].Length == 0
        ? source
        : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,
          Uri.UnescapeDataString(parts[0]).Replace('/', Path.DirectorySeparatorChar)));
      Assert.True(linked.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal),
        $"{page}: link leaves the repository: {target}");
      Assert.True(File.Exists(linked) || Directory.Exists(linked), $"{page}: missing link target {target}");
      if (parts.Length == 2 &&
          parts[1].Length != 0) {
        Assert.True(linked.EndsWith(".md", StringComparison.Ordinal),
          $"{page}: anchor on a non-Markdown file {target}");
        Assert.Contains(parts[1], Anchors(File.ReadAllText(linked)));
      }
    }
  }

  [Fact]
  public void DocumentationLivesInTheThreeMarkdownPlaces() {
    Assert.False(Directory.Exists(Path.Combine(Root, "docs")));
    Assert.Empty(Directory.EnumerateFiles(Root, "*.adoc"));
    Assert.Empty(Directory.EnumerateDirectories(Path.Combine(Root, "wiki")));
    var readme = File.ReadAllText(Path.Combine(Root, "README.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
    var headings = HeadingRegex.Matches(WithoutCode(readme)).Select(match => match.Value).ToArray();
    Assert.Equal("## License", headings[^1]);
    Assert.Contains("## A taste", headings, StringComparer.Ordinal);
    // The home page is the index: every other wiki page must be reachable from it.
    var home = File.ReadAllText(Path.Combine(Root, "wiki", "Home.md"));
    foreach (var page in Directory.EnumerateFiles(Path.Combine(Root, "wiki"), "*.md").Select(Path.GetFileName))
      if (!string.Equals(page, "Home.md", StringComparison.Ordinal)) {
        Assert.Contains($"]({page})", home, StringComparison.Ordinal);
      }
  }

  private static string WithoutCode(string markdown)
    => FenceRegex.Replace(markdown.Replace("\r\n", "\n", StringComparison.Ordinal), string.Empty);

  // GitHub heading anchors: lowercase, punctuation dropped, spaces turned into hyphens.
  private static HashSet<string> Anchors(string markdown) {
    var anchors = new HashSet<string>(StringComparer.Ordinal);
    foreach (Match match in HeadingRegex.Matches(WithoutCode(markdown))) {
      var anchor = new StringBuilder();
      foreach (var character in match.Groups["title"].Value.ToLowerInvariant())
        if (char.IsLetterOrDigit(character) ||
            character is '-' or '_') {
          anchor.Append(character);
        }
        else if (character == ' ') {
          anchor.Append('-');
        }

      anchors.Add(anchor.ToString());
    }

    return anchors;
  }
}
