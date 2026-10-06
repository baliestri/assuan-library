// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Generators.Tests;

public sealed class IncrementalityTests {
  private const string SOURCE = """
                                namespace Consumer {
                                  public sealed class State { }
                                  [AssuanLibrary.Attributes.AssuanHandlerGroup]
                                  public partial class Commands {
                                    [AssuanLibrary.Attributes.AssuanCommandHandler("ECHO", "Echoes arguments")]
                                    private static System.Threading.Tasks.ValueTask EchoAsync(
                                      AssuanLibrary.Server.CommandContext<State> ctx,
                                      System.Threading.CancellationToken ct) => ctx.SendDataAsync(ctx.Arguments, ct);
                                    [AssuanLibrary.Attributes.AssuanOptionHandler("pinentry-mode")]
                                    private static System.Threading.Tasks.ValueTask SetMode(
                                      AssuanLibrary.Server.OptionRequest request,
                                      AssuanLibrary.Server.CommandContext<State> context,
                                      System.Threading.CancellationToken token) => System.Threading.Tasks.ValueTask.CompletedTask;
                                  }
                                }
                                """;

  [Fact]
  public void UnrelatedEditKeepsHandlerAdapterAndHintStable() {
    var before = GeneratorHarness.Compile(SOURCE);
    var after = GeneratorHarness.Compile("internal class Unrelated { }\n" + SOURCE);
    Assert.Equal(2, before.GeneratedSources.Length);
    Assert.Equal(before.GeneratedSources, after.GeneratedSources);
    Assert.Equal(before.GeneratedHints, after.GeneratedHints);
    Assert.Empty(after.OutputCompilationErrors);
  }

  [Fact]
  public void ChangingPublicDescriptionUpdatesTheAffectedAdapterOnly() {
    var before = GeneratorHarness.Compile(SOURCE);
    var after = GeneratorHarness.Compile(SOURCE.Replace("Echoes arguments", "Returns arguments"));
    Assert.Equal(before.GeneratedHints, after.GeneratedHints);
    Assert.Equal(1, before.GeneratedSources.Zip(after.GeneratedSources)
      .Count(pair => pair.First != pair.Second));
    Assert.Empty(after.OutputCompilationErrors);
  }

  [Fact]
  public void ChangingAnOptionNameUpdatesOnlyTheGroupDispatcher() {
    var before = GeneratorHarness.Compile(SOURCE);
    var after = GeneratorHarness.Compile(SOURCE.Replace("pinentry-mode", "loopback-mode"));
    Assert.Equal(before.GeneratedHints, after.GeneratedHints);
    Assert.Equal(1, before.GeneratedSources.Zip(after.GeneratedSources)
      .Count(pair => pair.First != pair.Second));
    Assert.Empty(after.GeneratorDiagnostics);
  }
}
