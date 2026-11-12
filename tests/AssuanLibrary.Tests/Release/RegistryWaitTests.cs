// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AssuanLibrary.Tests.Release;

public sealed class RegistryWaitTests {
  private readonly static byte[] Package = "registry package bytes"u8.ToArray();

  [Theory]
  [InlineData("404,404,200")]
  [InlineData("503,429,200")]
  [InlineData("200")]
  public async Task AnInvisibleOrBusyRegistryIsWaitedForUntilThePackageAppears(string statuses) {
    await using var registry = new FakeRegistry(statuses);
    var target = NewTarget();
    var result = await RunAsync(registry.Uri, target, 0.5);
    Assert.True(result.ExitCode == 0, result.Output);
    Assert.Equal(Package, await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken));
    Assert.False(File.Exists(target + ".partial"));
    Assert.Equal(statuses.Split(',').Length, registry.Requests);
  }

  [Fact]
  public async Task AnExpiredWaitAsksForARerunWithoutLeavingAPartialDownload() {
    await using var registry = new FakeRegistry("404");
    var target = NewTarget();
    var result = await RunAsync(registry.Uri, target, 0.01);
    Assert.Equal(2, result.ExitCode);
    Assert.Contains("never upload again", result.Output, StringComparison.Ordinal);
    Assert.False(File.Exists(target));
    Assert.False(File.Exists(target + ".partial"));
    Assert.True(registry.Requests >= 1);
  }

  [Fact]
  public async Task AnUnexpectedRegistryAnswerStopsForInspection() {
    await using var registry = new FakeRegistry("403");
    var target = NewTarget();
    var result = await RunAsync(registry.Uri, target, 0.5);
    Assert.Equal(1, result.ExitCode);
    Assert.False(File.Exists(target));
    Assert.Equal(1, registry.Requests);
  }

  [Theory]
  [InlineData("http://example.invalid/package.nupkg", "2.0.1")]
  [InlineData("https://api.nuget.org/v3-flatcontainer/another/2.0.1/another.2.0.1.nupkg", "2.0.1")]
  [InlineData("", "2.0.1-rc.1")]
  public async Task OnlyTheLibraryFlatContainerAndStableVersionsAreProbed(string probe, string version) {
    var target = NewTarget();
    var result = await RunAsync(probe, target, 0.5, version);
    Assert.Equal(1, result.ExitCode);
    Assert.False(File.Exists(target));
  }

  private static string NewTarget()
    => Path.Combine(ReleaseFixture.ToolsRoot, "artifacts", "registry-wait-tests", Guid.NewGuid().ToString("N"),
      "AssuanLibrary.2.0.1.nupkg");

  private static async Task<(int ExitCode, string Output)> RunAsync(string probe, string target,
    double timeoutMinutes, string version = "2.0.1") {
    var start = new ProcessStartInfo("pwsh") {
      WorkingDirectory = ReleaseFixture.ToolsRoot, UseShellExecute = false,
      RedirectStandardOutput = true, RedirectStandardError = true
    };
    foreach (var argument in new[] {
               "-NoProfile", "-NonInteractive", "-File",
               Path.Combine(ReleaseFixture.ToolsRoot, "scripts", "release", "Wait-RegistryPackage.ps1"),
               "-Version", version, "-OutFile", target,
               "-TimeoutMinutes", timeoutMinutes.ToString(CultureInfo.InvariantCulture),
               "-InitialDelaySeconds", "0.05", "-MaxDelaySeconds", "0.1"
             })
      start.ArgumentList.Add(argument);

    if (probe.Length != 0) {
      start.ArgumentList.Add("-ProbeUri");
      start.ArgumentList.Add(probe);
    }

    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
    var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken)
      .WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken).ConfigureAwait(false);
    return (process.ExitCode, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
  }

  /// <summary>Loopback HTTP responder that answers each request with the next scripted status.</summary>
  private sealed class FakeRegistry : IAsyncDisposable {
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Task _serving;
    private readonly int[] _statuses;
    private readonly CancellationTokenSource _stop = new();
    private int _requests;

    internal FakeRegistry(string statuses) {
      _statuses = [.. statuses.Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture))];
      _listener.Start();
      Uri = string.Create(CultureInfo.InvariantCulture,
        $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/assuanlibrary.2.0.1.nupkg");
      _serving = ServeAsync();
    }

    internal string Uri { get; }
    internal int Requests => Volatile.Read(ref _requests);

    public async ValueTask DisposeAsync() {
      await _stop.CancelAsync().ConfigureAwait(false);
      _listener.Stop();
      try { await _serving.ConfigureAwait(false); }
      catch (Exception error) when (error is OperationCanceledException or SocketException
                                      or ObjectDisposedException) {
        /* The listener was stopped while waiting for another request. */
      }

      _stop.Dispose();
    }

    private async Task ServeAsync() {
      while (!_stop.IsCancellationRequested) {
        using var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
        var stream = client.GetStream();
        await using (stream.ConfigureAwait(false)) {
          await ReadRequestAsync(stream).ConfigureAwait(false);
          var index = Interlocked.Increment(ref _requests) - 1;
          var status = _statuses[Math.Min(index, _statuses.Length - 1)];
          var body = status == 200 ? Package : [];
          var head = string.Create(CultureInfo.InvariantCulture,
            $"HTTP/1.1 {status} Scripted\r\nContent-Type: application/octet-stream\r\n" +
            $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
          await stream.WriteAsync(Encoding.ASCII.GetBytes(head), _stop.Token).ConfigureAwait(false);
          await stream.WriteAsync(body, _stop.Token).ConfigureAwait(false);
          await stream.FlushAsync(_stop.Token).ConfigureAwait(false);
        }
      }
    }

    private async Task ReadRequestAsync(NetworkStream stream) {
      var buffer = new byte[4096];
      var received = new StringBuilder();
      while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal)) {
        var read = await stream.ReadAsync(buffer, _stop.Token).ConfigureAwait(false);
        if (read == 0) {
          return;
        }

        received.Append(Encoding.ASCII.GetString(buffer, 0, read));
      }
    }
  }
}
