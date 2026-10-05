// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Tests.Server;

public sealed class CompatibilityTests {
  [Theory]
  [InlineData("unfinished")]
  [InlineData("D data\n")]
  [InlineData("END\n")]
  [InlineData("CAN\n")]
  [InlineData("#bad\0\n")]
  public async Task InvalidOrPartialInputClosesWithoutAFinal(string input) {
    await using var stream = new SessionDuplexStream(Encoding.UTF8.GetBytes(input));
    var session = new ServerSession<object>(new AcceptedConnection(stream), new object(),
      new CommandRegistry<object>(), new DefaultSessionHooks<object>(), ServerOptions.Default);
    await Assert.ThrowsAsync<ServerException>(() => session.RunAsync(TestContext.Current.CancellationToken));
    Assert.Equal("OK\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task EscapedInquiryVectorsAndCancelOutcomeMatchWireBehavior() {
    await using var stream = new SessionDuplexStream(
      "ASK\n# comment\n\nD \nD SECRET_MARKER%00%ff%25\nEND\nASK\nCAN\nBYE\n"u8.ToArray());
    var registry = new CommandRegistry<object>();
    registry.Register(new Handler("ASK", async (context, token) => {
      await using var inquiry = await context.BeginInquiryAsync("VALUE", "challenge"u8.ToArray(),
        PayloadSensitivity.Secret, token);
      var count = 0;
      InquiryChunk? chunk;
      while ((chunk = await inquiry.ReadNextAsync(token)) is not null) {
        using (chunk) {
          count += chunk.Expose().Length;
        }
      }

      if (inquiry.Finish() == InquiryOutcome.Cancelled) {
        throw HandlerException.Remote(99, "cancelled");
      }

      await context.SendDataAsync(Encoding.ASCII.GetBytes(count.ToString(
        CultureInfo.InvariantCulture)), token);
    }));
    var session = new ServerSession<object>(new AcceptedConnection(stream), new object(), registry,
      new DefaultSessionHooks<object>(), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal("OK\nINQUIRE VALUE challenge\nD 16\nOK\nINQUIRE VALUE challenge\nERR 99 cancelled\nOK\n"u8.ToArray(),
      stream.WrittenBytes);
  }

  [Fact]
  public async Task ExplicitTransportFailureDoesNotProduceAnInternalFinal() {
    await using var stream = new SessionDuplexStream("FAIL\nBYE\n"u8.ToArray());
    var registry = new CommandRegistry<object>();
    registry.Register(new Handler("FAIL", (_, _) => ValueTask.FromException(
      new TransportException(TransportErrorKind.Io, new IOException("SECRET_MARKER")))));
    var session = new ServerSession<object>(new AcceptedConnection(stream), new object(), registry,
      new DefaultSessionHooks<object>(), ServerOptions.Default);
    var error = await Assert.ThrowsAsync<ServerException>(() => session.RunAsync(TestContext.Current.CancellationToken));
    Assert.Equal(ServerErrorKind.Transport, error.Kind);
    Assert.DoesNotContain("SECRET_MARKER", error.ToString());
    Assert.Equal("OK\n"u8.ToArray(), stream.WrittenBytes);
  }

  [Fact]
  public async Task FullUnsignedRemoteCodeIsPreservedOnTheWire() {
    await using var stream = new SessionDuplexStream("FAIL\nBYE\n"u8.ToArray());
    var registry = new CommandRegistry<object>();
    registry.Register(new Handler("FAIL", (_, _) => ValueTask.FromException(
      HandlerException.Remote(uint.MaxValue, "denied"))));
    var session = new ServerSession<object>(new AcceptedConnection(stream), new object(), registry,
      new DefaultSessionHooks<object>(), ServerOptions.Default);
    await session.RunAsync(TestContext.Current.CancellationToken);
    Assert.Equal("OK\nERR 4294967295 denied\nOK\n"u8.ToArray(), stream.WrittenBytes);
  }

  private sealed class Handler(string name, Func<CommandContext<object>, CancellationToken, ValueTask> call)
    : ICommandHandler<object> {
    public string Name => name;
    public string Description => "fixture";
    public ValueTask HandleAsync(CommandContext<object> context, CancellationToken token) => call(context, token);
  }
}
