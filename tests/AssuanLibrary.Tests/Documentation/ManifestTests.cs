// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.Documentation;

public sealed class ManifestTests {
  internal static readonly string _repository = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);
  internal static readonly string _manifest = Path.Combine(_repository, "docs", "site", "sources.json");

  [Fact]
  public void ReviewedManifestHasUniqueSortedInRepositorySourcesAndDestinations() {
    Assert.True(File.Exists(_manifest));
    using var json = JsonDocument.Parse(File.ReadAllText(_manifest));
    var sources = json.RootElement.GetProperty("sources").EnumerateArray().ToArray();
    Assert.NotEmpty(sources);
    var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var previous = 0;
    foreach (var item in sources) {
      var order = item.GetProperty("order").GetInt32();
      Assert.True(order > previous);
      previous = order;
      Assert.False(item.GetProperty("title").GetString() is null or "");
      var source = item.GetProperty("source").GetString()!;
      var destination = item.GetProperty("destination").GetString()!;
      Assert.True(sourcePaths.Add(source));
      Assert.True(destinations.Add(destination));
      Assert.False(Path.IsPathRooted(source) || source.Split('/', '\\').Contains(".."));
      Assert.False(Path.IsPathRooted(destination) || destination.Split('/', '\\').Contains(".."));
      Assert.True(File.Exists(Path.Combine(_repository, source)) || item.GetProperty("optional").GetBoolean());
      Assert.False(item.GetProperty("includeInFull").GetBoolean() &&
                   !item.GetProperty("includeInIndex").GetBoolean());
    }

    Assert.Contains(sources, entry => entry.GetProperty("source").GetString() == "docs/nuget/README.md");
  }

  [Fact]
  public void OperationalSourcesAreNotAccidentallyInTheFullUserCorpus() {
    using var json = JsonDocument.Parse(File.ReadAllText(_manifest));
    Assert.DoesNotContain(json.RootElement.GetProperty("sources").EnumerateArray(), item =>
      item.GetProperty("source").GetString() is { } source &&
      source.Contains("operations", StringComparison.OrdinalIgnoreCase) &&
      item.GetProperty("includeInFull").GetBoolean());
  }
}
