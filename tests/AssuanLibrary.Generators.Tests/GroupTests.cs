// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Generators.Tests;

public sealed class GroupTests {
  private const string SOURCE = """
                                namespace Consumer {
                                  public sealed class State { }
                                  [AssuanLibrary.Attributes.AssuanHandlerGroup]
                                  public static partial class Commands {
                                    [AssuanLibrary.Attributes.AssuanCommandHandler("ECHO", "Echo")]
                                    private static System.Threading.Tasks.ValueTask EchoAsync(
                                      AssuanLibrary.Server.CommandContext<State> context,
                                      System.Threading.CancellationToken token) => context.SendDataAsync(context.Arguments, token);
                                    [AssuanLibrary.Attributes.AssuanCommandHandler("VERSION", "Version")]
                                    private static System.Threading.Tasks.ValueTask VersionAsync(
                                      AssuanLibrary.Server.CommandContext<State> context,
                                      System.Threading.CancellationToken token) => System.Threading.Tasks.ValueTask.CompletedTask;
                                  }
                                  public static class Usage {
                                    public static void Configure() {
                                      var registry = new AssuanLibrary.Server.CommandRegistry<State>();
                                      Commands.RegisterHandlers(registry);
                                      var server = new AssuanLibrary.Server.AssuanServer<State>(() => new State(),
                                        AssuanLibrary.Server.ServerOptions.Default);
                                      Commands.RegisterHandlers(server);
                                    }
                                  }
                                }
                                """;

  [Fact]
  public void GroupGeneratesExplicitStableRegistrationForBothHandlers() {
    var result = GeneratorHarness.Compile(SOURCE);
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    Assert.Equal(3, result.GeneratedSources.Length);
    var group = Assert.Single(result.GeneratedSources, text => text.Contains("RegisterHandlers"));
    Assert.Contains("registry.Register(EchoAsyncHandler)", group);
    Assert.Contains("registry.Register(VersionAsyncHandler)", group);
    Assert.Contains("server.Register(EchoAsyncHandler)", group);
    Assert.DoesNotContain("CreateHooks", group);
  }

  [Fact]
  public void EmptyOrMixedStateGroupDoesNotGenerateInvalidRegistration() {
    var empty = GeneratorHarness.Compile("""
                                         [AssuanLibrary.Attributes.AssuanHandlerGroup]
                                         public partial class Empty { }
                                         """);
    Assert.Contains(empty.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ASG008");
    Assert.Empty(empty.GeneratedSources);
    var position = SOURCE.LastIndexOf("CommandContext<State>", StringComparison.Ordinal);
    Assert.True(position >= 0);
    var mixedSource = SOURCE[..position] + "CommandContext<object>" +
                      SOURCE[(position + "CommandContext<State>".Length)..];
    var mixed = GeneratorHarness.Compile(mixedSource);
    Assert.Contains(mixed.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ASG008");
    Assert.DoesNotContain(mixed.GeneratedSources, text => text.Contains("RegisterHandlers"));
  }

  [Fact]
  public void ExistingRegistrationMemberReportsGroupCollision() {
    var result = GeneratorHarness.Compile(SOURCE.Replace("public static partial class Commands {",
      "public static partial class Commands { public static void RegisterHandlers(object x) {}"));
    Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ASG002");
    Assert.DoesNotContain(result.GeneratedSources, text => text.Contains("RegisterHandlers"));
  }
}
