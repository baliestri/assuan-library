// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class BuiltinTests {
  [Fact]
  public async Task HelpSplitsUtf8CommentsWithinWireLimitWithoutWritingFinal() {
    await using var stream = new SessionDuplexStream(ReadOnlyMemory<byte>.Empty);
    await using var channel = new AssuanChannel(stream);
    var machine = new ServerMachine();
    machine.SendResponse(ProtocolResponseKind.Ok);
    machine.ReceiveRequest(RequestKind.Command);
    var registry = new CommandRegistry<object>();
    registry.Register(new EchoHandler(string.Concat(Enumerable.Repeat("á", 700))));
    registry.Freeze();
    var context = new CommandContext<object>(new object(), ReadOnlyMemory<byte>.Empty, null, true,
      channel, machine, new TransportDeadline(TimeSpan.FromSeconds(5)));
    Assert.False(await ServerBuiltins.ExecuteAsync("HELP", context, registry,
      new DefaultSessionHooks<object>(), TestContext.Current.CancellationToken));
    var output = stream.WrittenBytes;
    var start = 0;
    for (var index = 0; index < output.Length; ++index) {
      if (output[index] != (byte)'\n') {
        continue;
      }

      var line = output.AsSpan(start, index - start);
      Assert.True((line.Length + 1) <= 1000);
      Assert.StartsWith("# ", new UTF8Encoding(false, true).GetString(line));
      start = index + 1;
    }

    Assert.Equal(output.Length, start);
    Assert.DoesNotContain("OK\n", Encoding.UTF8.GetString(output));
  }

  private sealed class EchoHandler(string description) : ICommandHandler<object> {
    public string Name => "ECHO";
    public string Description => description;

    public ValueTask HandleAsync(CommandContext<object> context, CancellationToken cancellationToken)
      => ValueTask.CompletedTask;
  }
}
