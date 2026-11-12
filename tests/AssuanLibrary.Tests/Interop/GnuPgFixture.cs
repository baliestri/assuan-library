// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.Interop;

internal sealed class GnuPgFixture : IAsyncDisposable {
  private bool _started;

  internal GnuPgFixture(GnuPgTools tools) {
    Tools = tools;
    var root = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);
    Home = Path.Combine(root, "artifacts", "g", Guid.NewGuid().ToString("N")[..12]);
    Directory.CreateDirectory(Home);
    if (!OperatingSystem.IsWindows()) {
      File.SetUnixFileMode(Home, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
  }

  internal static bool Required {
    get
    {
      var value = Environment.GetEnvironmentVariable("ASSUAN_GNUPG_REQUIRED");
      return value switch {
        null => false,
        "1" => true,
        var _ => throw new InvalidOperationException("Invalid required interoperability setting.")
      };
    }
  }

  internal GnuPgTools Tools { get; }
  internal string Home { get; }

  public async ValueTask DisposeAsync() {
    try {
      if (_started) {
        await RunToolAsync(Tools.Gpgconf, null, CancellationToken.None, "--kill", "gpg-agent").ConfigureAwait(false);
      }

      Directory.Delete(Home, true);
    }
    catch (Exception error) {
      throw new InvalidOperationException("Isolated GnuPG cleanup failed.", error);
    }
  }

  internal async Task LaunchAgentAsync(CancellationToken token) {
    await RunToolAsync(Tools.Gpgconf, null, token, "--launch", "gpg-agent").ConfigureAwait(false);
    _started = true;
  }

  internal Task<string> RunPeerAsync(string socket, string commands, CancellationToken token)
    => RunToolAsync(Tools.GpgConnectAgent, commands, token, "--no-history", "--raw-socket", socket);

  private async Task<string> RunToolAsync(string executable, string? input, CancellationToken token,
    params string[] arguments) {
    using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
    limit.CancelAfter(TimeSpan.FromSeconds(20));
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false, RedirectStandardInput = true,
      RedirectStandardOutput = true, RedirectStandardError = true,
      Environment = { ["GNUPGHOME"] = Home },
      ArgumentList = { "--homedir", Home }
    };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);

    using var process = Process.Start(start) ?? throw new InvalidOperationException("GnuPG tool did not start.");
    try {
      var stdout = process.StandardOutput.ReadToEndAsync(limit.Token);
      var stderr = process.StandardError.ReadToEndAsync(limit.Token);
      if (input is not null) {
        await process.StandardInput.WriteAsync(input.AsMemory(), limit.Token).ConfigureAwait(false);
      }

      process.StandardInput.Close();
      await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
      var result = await stdout.ConfigureAwait(false);
      _ = await stderr.ConfigureAwait(false);
      if (process.ExitCode != 0 ||
          result.Length > 8192) {
        throw new InvalidOperationException("GnuPG tool returned an invalid result.");
      }

      return result;
    }
    finally {
      try {
        if (!process.HasExited) {
          process.Kill(true);
        }
      }
      catch (InvalidOperationException) {
        /* The process exited between inspection and cleanup. */
      }
    }
  }
}
