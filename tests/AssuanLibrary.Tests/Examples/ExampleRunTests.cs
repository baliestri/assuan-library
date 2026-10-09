// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AssuanLibrary.Examples;
using AssuanLibrary.Tests.Transport;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Unix;

namespace AssuanLibrary.Tests.Examples;

public sealed class ExampleRunTests {
  [Theory]
  [InlineData("sexpressions", "canonical-bytes=")]
  [InlineData("custom-transport", "decoded-bytes=2")]
  public async Task SelfContainedExamplesCompleteWithoutExposingBinaryPayload(string name, string expected) {
    await using var output = new StringWriter();
    await using var error = new StringWriter();
    var code = await ExampleCatalog.RunAsync(["--example", name], new StringReader(""), output, error,
      TestContext.Current.CancellationToken);
    Assert.Equal(0, code);
    Assert.Contains(expected, output.ToString());
    Assert.DoesNotContain("SECRET_MARKER", output.ToString());
    Assert.Empty(error.ToString());
  }

  [Theory]
  [InlineData("tcp-client", "--port", "0")]
  [InlineData("tcp-server", "--port", "65536")]
  [InlineData("agent-info", "--homedir", "private")]
  public async Task InvalidArgumentsFailBeforeTransportOrProcessIo(string name, string flag, string value) {
    await using var output = new StringWriter();
    await using var error = new StringWriter();
    var code = await ExampleCatalog.RunAsync(["--example", name, flag, value], new StringReader(""),
      output, error, TestContext.Current.CancellationToken);
    Assert.NotEqual(0, code);
    Assert.Empty(output.ToString());
    Assert.Contains(name == "agent-info" ? "--gpgconf" : "--port", error.ToString());
    Assert.DoesNotContain("private", error.ToString());
  }

  [Fact]
  public async Task TcpClientAndServerShareAnExplicitLoopbackPortAndStopCooperatively() {
    var reservation = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    reservation.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    var port = ((IPEndPoint)reservation.LocalEndPoint!).Port;
    reservation.Dispose();
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    stop.CancelAfter(TimeSpan.FromSeconds(5));
    await using var serverOutput = new StringWriter();
    await using var serverError = new StringWriter();
    var serving = ExampleCatalog.RunAsync([
      "--example", "tcp-server", "--port", port.ToString(
        CultureInfo.InvariantCulture)
    ], new StringReader(""), serverOutput, serverError, stop.Token);
    try {
      await using var clientOutput = new StringWriter();
      await using var clientError = new StringWriter();
      var code = await ExampleCatalog.RunAsync([
        "--example", "tcp-client", "--port", port.ToString(
          CultureInfo.InvariantCulture)
      ], new StringReader(""), clientOutput, clientError, stop.Token);
      Assert.Equal(0, code);
      Assert.Contains("data-bytes=6", clientOutput.ToString());
      Assert.Empty(clientError.ToString());
    }
    finally { await stop.CancelAsync(); }

    Assert.Equal(0, await serving.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
    Assert.Contains("pending sessions=0", serverOutput.ToString());
    Assert.Empty(serverError.ToString());
  }

  [Fact]
  public async Task ExistingAgentExampleUsesControlledDiscoveryWithoutPrintingPrivateData() {
    using var fixture = new AgentFixture();
    await using var output = new StringWriter();
    await using var error = new StringWriter();
    var token = TestContext.Current.CancellationToken;
    const string REPLY = "D 2.51\nOK\n";
    if (OperatingSystem.IsWindows()) {
      using var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      var port = ((IPEndPoint)listener.LocalEndpoint).Port;
      var nonce = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
      File.WriteAllBytes(fixture.SocketFile,
        [.. Encoding.ASCII.GetBytes(port + "\n"), .. nonce]);
      var running = ExampleCatalog.RunAsync([
        "--example", "agent-info", "--gpgconf", fixture.Executable,
        "--homedir", fixture.Home
      ], new StringReader(""), output, error, token);
      using var peer = await listener.AcceptTcpClientAsync(token);
      var received = new byte[16];
      await peer.GetStream().ReadExactlyAsync(received, token);
      Assert.Equal(nonce, received);
      var command = new byte[16];
      await peer.GetStream().WriteAsync("OK\n"u8.ToArray(), token);
      await peer.GetStream().ReadExactlyAsync(command, token);
      Assert.Equal("GETINFO version\n"u8.ToArray(), command);
      await peer.GetStream().WriteAsync(Encoding.ASCII.GetBytes(REPLY), token);
      Assert.Equal(0, await running);
    }
    else if (OperatingSystem.IsLinux() ||
             OperatingSystem.IsMacOS()) {
      File.SetUnixFileMode(fixture.Home, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
      await using var listener = UnixAcceptor.Bind(LocalEndpoint.Unix(fixture.SocketFile));
      var running = ExampleCatalog.RunAsync([
        "--example", "agent-info", "--gpgconf", fixture.Executable,
        "--homedir", fixture.Home
      ], new StringReader(""), output, error, token);
      await using var peer = (await listener.AcceptAsync(token)).Stream;
      await peer.WriteAsync("OK\n"u8.ToArray(), token);
      var command = new byte[16];
      await peer.ReadExactlyAsync(command, token);
      Assert.Equal("GETINFO version\n"u8.ToArray(), command);
      await peer.WriteAsync(Encoding.ASCII.GetBytes(REPLY), token);
      Assert.Equal(0, await running);
      listener.Cleanup();
    }

    Assert.Contains("version-bytes=4", output.ToString());
    Assert.DoesNotContain(fixture.Home, output.ToString());
    Assert.Empty(error.ToString());
  }
}
