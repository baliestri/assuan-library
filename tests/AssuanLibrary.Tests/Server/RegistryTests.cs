// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using AssuanLibrary.Server;

namespace AssuanLibrary.Tests.Server;

public sealed class RegistryTests {
  [Fact]
  public void RegistrationIsCaseSensitiveAndDoesNotReplaceOnFailure() {
    var registry = new CommandRegistry<object>();
    var first = new Handler("ECHO");
    registry.Register(first);
    registry.Register(new Handler("echo"));
    Assert.Same(first, registry.Find("ECHO"));
    Assert.NotSame(first, registry.Find("echo"));
    Assert.Equal(RegistryErrorKind.Duplicate,
      Assert.Throws<RegistryException>(() => registry.Register(new Handler("ECHO"))).Kind);
    Assert.Same(first, registry.Find("ECHO"));
    registry.Freeze();
    Assert.Equal(RegistryErrorKind.Frozen,
      Assert.Throws<RegistryException>(() => registry.Register(new Handler("OTHER"))).Kind);
  }

  [Theory]
  [InlineData("NOP", RegistryErrorKind.Reserved)]
  [InlineData("BYE", RegistryErrorKind.Reserved)]
  [InlineData("HELP", RegistryErrorKind.Reserved)]
  [InlineData("RESET", RegistryErrorKind.Reserved)]
  [InlineData("OPTION", RegistryErrorKind.Reserved)]
  [InlineData("bad name", RegistryErrorKind.InvalidName)]
  [InlineData("", RegistryErrorKind.InvalidName)]
  public void RejectsReservedAndInvalidNames(string name, RegistryErrorKind expected) {
    var registry = new CommandRegistry<object>();
    Assert.Equal(expected, Assert.Throws<RegistryException>(() => registry.Register(new Handler(name))).Kind);
  }

  [Theory]
  [InlineData("bad\nhelp")]
  [InlineData("bad\rhelp")]
  [InlineData("bad\0help")]
  public void RejectsMultilineAndNulDescriptionsWithoutDisclosingThem(string description) {
    var registry = new CommandRegistry<object>();
    var error = Assert.Throws<RegistryException>(() => registry.Register(new Handler("ECHO", description)));
    Assert.Equal(RegistryErrorKind.InvalidDescription, error.Kind);
    Assert.DoesNotContain("bad", error.ToString());
  }

  [Fact]
  public void RejectsTokenThatWouldNotFitInOneWireLine() {
    var registry = new CommandRegistry<object>();
    Assert.Equal(RegistryErrorKind.InvalidName,
      Assert.Throws<RegistryException>(() => registry.Register(new Handler(new string('X', 1000)))).Kind);
  }

  [Fact]
  public void RemoteErrorValidatesItsCompleteWireLineBeforeItCanBeSent() {
    var valid = HandlerException.Remote(55, "invalid value");
    Assert.Equal(HandlerErrorKind.Remote, valid.Kind);
    Assert.DoesNotContain("invalid value", valid.ToString());
    Assert.Equal(HandlerErrorKind.Internal, HandlerException.Internal().Kind);
    Assert.Throws<ArgumentException>(() => HandlerException.Remote(55, "private\nvalue"));
    Assert.Throws<ArgumentException>(() => HandlerException.Remote(55, new string('x', 1000)));
    foreach (var code in new uint[] { 0, 1, uint.MaxValue }) {
      var budget = 1000 - "ERR ".Length - code.ToString(CultureInfo.InvariantCulture).Length - 2;
      Assert.Equal(HandlerErrorKind.Remote, HandlerException.Remote(code, new string('x', budget)).Kind);
      Assert.Throws<ArgumentException>(() => HandlerException.Remote(code, new string('x', budget + 1)));
    }
  }

  private sealed class Handler(string name, string description = "test") : ICommandHandler<object> {
    public string Name { get; } = name;
    public string Description { get; } = description;

    public ValueTask HandleAsync(CommandContext<object> context, CancellationToken cancellationToken)
      => ValueTask.CompletedTask;
  }
}
