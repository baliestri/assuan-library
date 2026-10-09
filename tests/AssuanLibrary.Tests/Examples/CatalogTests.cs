// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Examples;

namespace AssuanLibrary.Tests.Examples;

public sealed class CatalogTests {
  [Fact]
  public void ReadmeDocumentsAllExampleEntryPointsAndTheBreakingVersion() {
    var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    var readme = File.ReadAllText(Path.Combine(repository, "README.adoc"));
    foreach (var name in new[] { "sexpressions", "tcp-server", "tcp-client", "custom-transport", "agent-info" }) {
      Assert.Contains("--example " + name, readme);
    }

    Assert.Contains("--list", readme);
    Assert.Contains("2.0.0", readme);
    Assert.Contains("dotnet run --project examples/AssuanLibrary.Examples", readme);
  }

  [Fact]
  public async Task ListAndHelpAreAvailableWithoutOpeningAConnection() {
    await using var output = new StringWriter();
    await using var error = new StringWriter();
    var result = await ExampleCatalog.RunAsync(["--list"], new StringReader(""), output, error,
      TestContext.Current.CancellationToken);
    Assert.Equal(0, result);
    foreach (var name in new[] { "sexpressions", "tcp-server", "tcp-client", "custom-transport", "agent-info" }) {
      Assert.Contains(name, output.ToString());
    }

    Assert.Equal(5, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    Assert.Empty(error.ToString());
  }

  [Theory]
  [InlineData("--example")]
  [InlineData("--example missing")]
  [InlineData("--unknown")]
  public async Task InvalidCliReturnsNonzeroAndActionableHelp(string commandLine) {
    await using var output = new StringWriter();
    await using var error = new StringWriter();
    var result = await ExampleCatalog.RunAsync(commandLine.Split(' '), new StringReader(""), output, error,
      TestContext.Current.CancellationToken);
    Assert.NotEqual(0, result);
    Assert.Contains("--list", error.ToString());
    Assert.DoesNotContain("Exception", error.ToString());
  }

  [Fact]
  public async Task InteractiveMenuCanSelectTheSamePrimitiveExample() {
    await using var output = new StringWriter();
    await using var error = new StringWriter();
    var code = await ExampleCatalog.RunAsync([], new StringReader("1\n"), output, error,
      TestContext.Current.CancellationToken);
    Assert.Equal(0, code);
    Assert.Contains("sexpressions", output.ToString());
    Assert.Empty(error.ToString());
  }
}
