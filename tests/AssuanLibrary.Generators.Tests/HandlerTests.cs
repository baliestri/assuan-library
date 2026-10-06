// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Generators.Tests;

public sealed class HandlerTests {
  private const string VALID = """
                               namespace Consumer {
                                 public sealed class State { }
                                 public partial class Demo {
                                   [AssuanLibrary.Attributes.AssuanCommandHandler("ECHO", "Echoes arguments")]
                                   private static System.Threading.Tasks.ValueTask EchoAsync(
                                     AssuanLibrary.Server.CommandContext<State> ctx,
                                     System.Threading.CancellationToken ct) => ctx.SendDataAsync(ctx.Arguments, ct);
                                 }
                               }
                               """;

  [Fact]
  public void ValidPrivateStaticHandlerEmitsACompilableExplicitAdapter() {
    var result = GeneratorHarness.Compile(VALID);
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    var text = Assert.Single(result.GeneratedSources);
    Assert.Contains("ICommandHandler<global::Consumer.State>", text);
    Assert.Contains("EchoAsyncHandler", text);
    Assert.Contains("EchoAsync(context, token)", text);
    Assert.DoesNotContain("Register(", text); // No automatic registration.
  }

  [Fact]
  public async Task GeneratedAdapterRegistersAndRunsOneLiteralAssuanCommand() {
    var generated = GeneratorHarness.Compile(VALID);
    using var image = new MemoryStream();
    var emitted = generated.OutputCompilation.Emit(image,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
    var assembly = Assembly.Load(image.ToArray());
    var state = assembly.GetType("Consumer.State")!;
    var demo = assembly.GetType("Consumer.Demo")!;
    var adapter = demo.GetProperty("EchoAsyncHandler")!.GetValue(null)!;
    var registryType = typeof(CommandRegistry<>).MakeGenericType(state);
    var registry = Activator.CreateInstance(registryType)!;
    registryType.GetMethod("Register")!.Invoke(registry, [adapter]);
    await using var peer = new Duplex("ECHO abc\nBYE\n"u8.ToArray());
    var hooks = Activator.CreateInstance(typeof(DefaultSessionHooks<>).MakeGenericType(state))!;
    var sessionType = typeof(ServerSession<>).MakeGenericType(state);
    var session = Activator.CreateInstance(sessionType,
      new AcceptedConnection(peer), Activator.CreateInstance(state), registry,
      hooks, ServerOptions.Default)!;
    var run = (Task)sessionType.GetMethod("RunAsync")!.Invoke(session,
      [TestContext.Current.CancellationToken])!;
    await run;
    Assert.Equal("OK\nD abc\nOK\nOK\n"u8.ToArray(), peer.Written);
  }

  [Theory]
  [InlineData("private static System.Threading.Tasks.ValueTask EchoAsync", "private static System.Threading.Tasks.Task EchoAsync")]
  [InlineData("private static System.Threading.Tasks.ValueTask EchoAsync", "private System.Threading.Tasks.ValueTask EchoAsync")]
  [InlineData("System.Threading.CancellationToken ct", "int ct")]
  [InlineData("EchoAsync(\n", "EchoAsync<T>(\n")]
  public void InvalidHandlerSignatureIsDiagnosedWithoutGeneratedAdapter(string original, string replacement) {
    var source = VALID.Replace(original, replacement);
    var result = GeneratorHarness.Compile(source);
    Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ASG006");
    Assert.Empty(result.GeneratedSources);
  }

  [Theory]
  [InlineData("NOP", "No operation")]
  [InlineData("bad name", "Echo")]
  [InlineData("ECHO", "bad\nhelp")]
  public void BadMetadataIsRedactedAndDoesNotGenerateAHandler(string name, string description) {
    var source = VALID.Replace("\"ECHO\", \"Echoes arguments\"",
      "\"" + name + "\", \"" + description.Replace("\n", "\\n") + "\"");
    var result = GeneratorHarness.Compile(source);
    Assert.Contains(result.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ASG007");
    Assert.Empty(result.GeneratedSources);
    Assert.DoesNotContain("bad name", string.Join(" ", result.GeneratorDiagnostics.Select(item => item.ToString())));
  }

  [Fact]
  public void ExistingGeneratedPropertyAndNonpartialContainerAreDiagnosed() {
    var duplicate = GeneratorHarness.Compile(VALID.Replace("public partial class Demo {",
      "public partial class Demo { public static int EchoAsyncHandler => 1;"));
    Assert.Contains(duplicate.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ASG002");
    Assert.Empty(duplicate.GeneratedSources);
    var nonpartial = GeneratorHarness.Compile(VALID.Replace("public partial class Demo", "public class Demo"));
    Assert.Contains(nonpartial.GeneratorDiagnostics, diagnostic => diagnostic.Id == "ASG001");
    Assert.Empty(nonpartial.GeneratedSources);
  }

  [Fact]
  public void PrivateStateTypeCannotLeakThroughGeneratedPublicHandlerProperty() {
    var result = GeneratorHarness.Compile("""
                                          public partial class Demo {
                                            private sealed class PrivateState { }
                                            [AssuanLibrary.Attributes.AssuanCommandHandler("ECHO", "Echo")]
                                            private static System.Threading.Tasks.ValueTask EchoAsync(
                                              AssuanLibrary.Server.CommandContext<PrivateState> context,
                                              System.Threading.CancellationToken token) => System.Threading.Tasks.ValueTask.CompletedTask;
                                          }
                                          """);
    Assert.Contains(result.GeneratorDiagnostics, item => item.Id == "ASG006");
    Assert.Empty(result.GeneratedSources);
    Assert.Empty(result.OutputCompilationErrors);
  }

  [Fact]
  public void StaticPartialContainerProducesACompilableHandlerAdapter() {
    var result = GeneratorHarness.Compile(VALID.Replace("public partial class Demo", "public static partial class Demo"));
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    Assert.Contains("static partial class Demo", Assert.Single(result.GeneratedSources));
  }

  private sealed class Duplex(byte[] input) : Stream {
    private readonly MemoryStream _input = new(input, false);
    private readonly MemoryStream _output = new();
    internal byte[] Written => _output.ToArray();
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
      => _input.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
      => _input.ReadAsync(buffer, token);

    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
      => _output.WriteAsync(buffer, token);

    public override void Flush()
      => _output.Flush();

    public override Task FlushAsync(CancellationToken token)
      => _output.FlushAsync(token);

    public override long Seek(long offset, SeekOrigin origin)
      => throw new NotSupportedException();

    public override void SetLength(long value)
      => throw new NotSupportedException();
  }
}
