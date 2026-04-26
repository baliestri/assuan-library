// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Server.Dispatching;

namespace AssuanLibrary.Tests.Server;

public sealed class CommandHandlerRegistryTests {
  [Fact]
  public void Add_ShouldRejectDuplicateHandlers_CaseInsensitive() {
    var registry = new CommandHandlerRegistry();

    registry.Add(new TestHandler("TEST"));

    Should.Throw<InvalidOperationException>(() => registry.Add(new TestHandler("test")));
  }

  [Fact]
  public void TryGet_ShouldResolveHandlers_CaseInsensitive() {
    var expected = new TestHandler("Ping");
    var registry = new CommandHandlerRegistry([expected]);

    var found = registry.TryGet("PING", out var handler);

    found.ShouldBeTrue();
    handler.ShouldBeSameAs(expected);
  }

  [Fact]
  public void TryGet_ShouldReturnFalse_WhenHandlerIsMissing() {
    var registry = new CommandHandlerRegistry();

    var found = registry.TryGet("MISSING", out var _);

    found.ShouldBeFalse();
  }

  private sealed class TestHandler(string name) : CommandHandler {
    public override string Name => name;

    public override void Handle(IReadOnlyAssuanCommand command, IServerContext serverContext)
      => throw new NotSupportedException();
  }
}
