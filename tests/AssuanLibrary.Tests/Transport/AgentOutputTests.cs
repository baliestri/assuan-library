// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Transport.Discovery;

namespace AssuanLibrary.Tests.Transport;

public sealed class AgentOutputTests {
  [Fact]
  public void UnixPathsKeepLiteralPercentSpacesAndNonUtf8Bytes() {
    var bytes = new byte[] {
      (byte)'/', (byte)'s', (byte)'o', (byte)'c', (byte)'k', (byte)'e',
      (byte)'t', (byte)' ', (byte)'%', (byte)'2', (byte)'0', 0xff, (byte)'\n'
    };
    var path = AgentOutputParser.ParseUnix(bytes);
    Assert.Equal(bytes.AsSpan(..^1).ToArray(), path);
    Assert.True(path.AsSpan().EndsWith(new byte[] { (byte)'%', (byte)'2', (byte)'0', 0xff }));
  }

  [Theory]
  [InlineData("")]
  [InlineData("relative\n")]
  [InlineData("/two\n/lines\n")]
  [InlineData("/nul\0path\n")]
  [InlineData("/unterminated")]
  public void RejectsMalformedOutputWithoutEchoingIt(string value) {
    var output = Encoding.UTF8.GetBytes(value);
    var error = Assert.Throws<DiscoveryException>(() => AgentOutputParser.ParseUnix(output));
    Assert.Equal(DiscoveryErrorKind.InvalidOutput, error.Kind);
    if (value.Length != 0) {
      Assert.DoesNotContain(value, error.ToString());
    }
  }

  [Fact]
  public void WindowsAllowsOnlyStrictUtf8AbsolutePathAndFinalCrLf() {
    var output = "C:\\socket %20 file\r\n"u8;
    Assert.Equal(@"C:\socket %20 file", AgentOutputParser.ParseWindows(output));
    Assert.Equal(@"\\server\share\socket", AgentOutputParser.ParseWindows("\\\\server\\share\\socket\n"u8));
    byte[] invalid = [(byte)'C', (byte)':', (byte)'\\', 0xff, (byte)'\n'];
    Assert.Equal(DiscoveryErrorKind.InvalidOutput,
      Assert.Throws<DiscoveryException>(() => AgentOutputParser.ParseWindows(invalid)).Kind);
    Assert.Equal(DiscoveryErrorKind.InvalidOutput,
      Assert.Throws<DiscoveryException>(() => AgentOutputParser.ParseWindows("relative\n"u8)).Kind);
    Assert.Equal(DiscoveryErrorKind.InvalidOutput,
      Assert.Throws<DiscoveryException>(() => AgentOutputParser.ParseWindows("\\\\server\\\n"u8)).Kind);
  }
}
