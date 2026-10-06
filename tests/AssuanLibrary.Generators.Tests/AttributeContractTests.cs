// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Attributes;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;
using AssuanLibrary.SExpressions;

namespace AssuanLibrary.Generators.Tests;

public sealed class AttributeContractTests {
  [Fact]
  public void CommandHandlerAttributeBelongsToTheMainAssemblyWithoutLiteralAttributes() {
    Assert.Equal(typeof(ICommandHandler<>).Assembly,
      typeof(AssuanCommandHandlerAttribute).Assembly);
    Assert.Equal("ECHO", new AssuanCommandHandlerAttribute("ECHO", "Echo").CommandName);
    Assert.DoesNotContain(typeof(AssuanCommandHandlerAttribute).Assembly.GetExportedTypes(), type =>
      type.Name is "AssuanCommandLiteralAttribute" or "AssuanSExpressionLiteralAttribute");
  }

  [Fact]
  public void UnannotatedConsumerProducesNoGeneratedCodeOrErrors() {
    var result = GeneratorHarness.Compile("public class Empty {}", true);
    Assert.Empty(result.GeneratedSources);
    Assert.Empty(result.OutputCompilationErrors);
  }

  [Fact]
  public void ConsumerCanResolveAllAttributeTypes() {
    var result = GeneratorHarness.Compile("""
                                          using AssuanLibrary.Attributes;
                                          public partial class Demo {
                                            [AssuanCommandHandler("ECHO", "Echoes arguments")]
                                            private static System.Threading.Tasks.ValueTask EchoAsync(
                                              AssuanLibrary.Server.CommandContext<object> ctx,
                                              System.Threading.CancellationToken ct) => System.Threading.Tasks.ValueTask.CompletedTask;
                                          }
                                          """);
    Assert.Empty(result.OutputCompilationErrors);
    Assert.Single(result.GeneratedSources);
  }

  [Fact]
  public void CanonicalParsersRemainAvailableWithoutGeneratorLiteralAnnotations() {
    Assert.True(ProtocolCommand.Parse("NOP"u8).NameBytes.SequenceEqual("NOP"u8));
    Assert.True(SExpressionParser.ParseComplete("0:"u8).IsAtom);
  }
}
