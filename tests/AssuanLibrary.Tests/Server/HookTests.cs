// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Server;

namespace AssuanLibrary.Tests.Server;

public sealed class HookTests {
  [Fact]
  public void OptionParserDistinguishesMissingAndEmptyValues() {
    var empty = OptionRequest.Parse("--flag="u8.ToArray());
    Assert.Equal("flag"u8.ToArray(), empty.Name.ToArray());
    Assert.True(empty.Value.HasValue);
    Assert.Empty(empty.Value.Value.ToArray());
    Assert.Null(OptionRequest.Parse("flag"u8.ToArray()).Value);
    var spaced = OptionRequest.Parse("  --name =  x%20  "u8.ToArray());
    Assert.Equal("name"u8.ToArray(), spaced.Name.ToArray());
    Assert.Equal("x%20"u8.ToArray(), spaced.Value!.Value.ToArray());
  }

  [Theory]
  [InlineData("--")]
  [InlineData("=value")]
  [InlineData("bad\0name")]
  [InlineData("bad\nname")]
  [InlineData("na%me")]
  public void MalformedOptionsFailWithoutEchoingInput(string text) {
    var error = Assert.Throws<ProtocolException>(() => OptionRequest.Parse(System.Text.Encoding.UTF8.GetBytes(text)));
    Assert.DoesNotContain(text, error.ToString());
  }

  [Fact]
  public async Task DefaultHookRejectsUnknownOptionsWithTheExpectedCode() {
    var hooks = new DefaultSessionHooks<object>();
    var request = OptionRequest.Parse("color=red"u8.ToArray());
    var error = await Assert.ThrowsAsync<HandlerException>(async () =>
      await hooks.OptionAsync(request, null!, TestContext.Current.CancellationToken));
    Assert.Equal((uint)174, error.Code);
  }
}
