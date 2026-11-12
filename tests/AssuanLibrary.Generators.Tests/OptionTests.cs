// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Generators.Tests;

public sealed class OptionTests {
  private const string SOURCE = """
    public sealed class State { }
    [AssuanLibrary.Attributes.AssuanHandlerGroup]
    public static partial class Options {
      [AssuanLibrary.Attributes.AssuanOptionHandler("pinentry-mode")]
      private static System.Threading.Tasks.ValueTask SetMode(
        AssuanLibrary.Server.OptionRequest request,
        AssuanLibrary.Server.CommandContext<State> context,
        System.Threading.CancellationToken token) => System.Threading.Tasks.ValueTask.CompletedTask;
    }
    public static class Usage {
      public static AssuanLibrary.Server.ISessionHooks<State> Create() =>
        Options.CreateHooks(new AssuanLibrary.Server.DefaultSessionHooks<State>());
    }
    """;

  [Fact]
  public void OptionOnlyGroupCompilesAndDelegatesOtherHooksExplicitly() {
    var result = GeneratorHarness.Compile(SOURCE);
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    var generated = Assert.Single(result.GeneratedSources);
    Assert.Contains("CreateHooks", generated);
    Assert.Contains("MemoryExtensions.SequenceEqual", generated);
    Assert.Contains("_inner.AuthenticateAsync", generated);
    Assert.Contains("_inner.ResetAsync", generated);
    Assert.Contains("_inner.ClosedAsync", generated);
    Assert.Contains("_inner.OptionAsync", generated);
    Assert.DoesNotContain("new global::AssuanLibrary.Server.DefaultSessionHooks", generated);
  }

  [Theory]
  [InlineData("pinentry%mode", "ASG010")]
  [InlineData("--pinentry-mode", "ASG010")]
  public void InvalidOptionNamesProduceRedactedDiagnostics(string name, string id) {
    var result = GeneratorHarness.Compile(SOURCE.Replace("\"pinentry-mode\"", "\"" + name + "\""));
    Assert.Contains(result.GeneratorDiagnostics, item => string.Equals(item.Id, id, StringComparison.Ordinal));
    Assert.DoesNotContain(result.GeneratedSources, text => text.Contains("CreateHooks"));
    Assert.DoesNotContain(name, string.Join("\n", result.GeneratorDiagnostics));
  }

  [Fact]
  public void OptionMethodOutsideGroupOrWithWrongSignatureIsRejected() {
    var ungrouped = GeneratorHarness.Compile(SOURCE.Replace("[AssuanLibrary.Attributes.AssuanHandlerGroup]", ""));
    Assert.Contains(ungrouped.GeneratorDiagnostics, item => string.Equals(item.Id, "ASG010", StringComparison.Ordinal));
    var invalid = GeneratorHarness.Compile(SOURCE.Replace("System.Threading.Tasks.ValueTask SetMode",
      "System.Threading.Tasks.Task SetMode"));
    Assert.Contains(invalid.GeneratorDiagnostics, item => string.Equals(item.Id, "ASG009", StringComparison.Ordinal));
    Assert.DoesNotContain(invalid.GeneratedSources, text => text.Contains("CreateHooks"));
  }

  [Fact]
  public void DuplicateOptionNamesAndMixedStateInvalidateGroup() {
    var duplicate = GeneratorHarness.Compile(SOURCE.Replace("public static partial class Options {", """
      public static partial class Options {
        [AssuanLibrary.Attributes.AssuanOptionHandler("pinentry-mode")]
        private static System.Threading.Tasks.ValueTask Again(
          AssuanLibrary.Server.OptionRequest request,
          AssuanLibrary.Server.CommandContext<State> context,
          System.Threading.CancellationToken token) => System.Threading.Tasks.ValueTask.CompletedTask;
      """));
    Assert.Contains(duplicate.GeneratorDiagnostics, item => string.Equals(item.Id, "ASG010", StringComparison.Ordinal));
    var mixed = GeneratorHarness.Compile(SOURCE
      .Replace("CommandContext<State> context", "CommandContext<object> context")
      .Replace("public static partial class Options {", """
        public static partial class Options {
          [AssuanLibrary.Attributes.AssuanCommandHandler("ECHO", "Echo")]
          private static System.Threading.Tasks.ValueTask Echo(
            AssuanLibrary.Server.CommandContext<State> context,
            System.Threading.CancellationToken token) => System.Threading.Tasks.ValueTask.CompletedTask;
        """));
    Assert.Contains(mixed.GeneratorDiagnostics, item => string.Equals(item.Id, "ASG008", StringComparison.Ordinal));
  }

  [Fact]
  public void ExistingGeneratedPrivateHookNameIsDiagnosedBeforeEmission() {
    var result = GeneratorHarness.Compile(SOURCE.Replace("public static partial class Options {",
      "public static partial class Options { private sealed class __AssuanOptionHooks { }"));
    Assert.Contains(result.GeneratorDiagnostics, item => string.Equals(item.Id, "ASG002", StringComparison.Ordinal));
    Assert.DoesNotContain(result.GeneratedSources, text => text.Contains("CreateHooks"));
  }
}
