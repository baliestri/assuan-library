// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.Interop;

internal sealed class GnuPgTools {
  private GnuPgTools(string gpgconf, string gpgAgent, string gpgConnectAgent) {
    Gpgconf = gpgconf;
    GpgAgent = gpgAgent;
    GpgConnectAgent = gpgConnectAgent;
  }

  internal string Gpgconf { get; }
  internal string GpgAgent { get; }
  internal string GpgConnectAgent { get; }

  internal static GnuPgTools FromEnvironment() {
    if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") {
      throw new InvalidOperationException("Real GnuPG interoperability requires an isolated hosted runner.");
    }

    return Require(Environment.GetEnvironmentVariable("ASSUAN_GPGCONF_PATH"),
      Environment.GetEnvironmentVariable("ASSUAN_GPG_AGENT_PATH"),
      Environment.GetEnvironmentVariable("ASSUAN_GPG_CONNECT_AGENT_PATH"));
  }

  internal static GnuPgTools Require(string? gpgconf, string? gpgAgent, string? gpgConnectAgent) {
    var paths = new[] { gpgconf, gpgAgent, gpgConnectAgent };
    if (paths.Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))) {
      throw new InvalidOperationException("Required GnuPG executables are unavailable.");
    }

    var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    if (paths.Select(Path.GetDirectoryName).Distinct(comparer).Count() == 1) {
      return new GnuPgTools(gpgconf!, gpgAgent!, gpgConnectAgent!);
    }

    throw new InvalidOperationException("GnuPG executables must come from one installation.");
  }
}

internal sealed class GnuPgFixture : IAsyncDisposable {
  private bool _started;

  internal GnuPgFixture(GnuPgTools tools) {
    Tools = tools;
    var root = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);
    Home = Path.Combine(root, "artifacts", "interop-tests", Guid.NewGuid().ToString("N")[..12]);
    Directory.CreateDirectory(Home);
    if (!OperatingSystem.IsWindows()) {
      File.SetUnixFileMode(Home, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
  }

  internal static bool Required {
    get {
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
        await RunToolAsync(Tools.Gpgconf, null, CancellationToken.None, "--kill", "gpg-agent");
      }

      Directory.Delete(Home, true);
    }
    catch {
      throw new InvalidOperationException("Isolated GnuPG cleanup failed.");
    }
  }

  internal async Task LaunchAgentAsync(CancellationToken token) {
    await RunToolAsync(Tools.Gpgconf, null, token, "--launch", "gpg-agent");
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
      RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.Environment["GNUPGHOME"] = Home;
    start.ArgumentList.Add("--homedir");
    start.ArgumentList.Add(Home);
    foreach (var argument in arguments) {
      start.ArgumentList.Add(argument);
    }

    using var process = Process.Start(start) ?? throw new InvalidOperationException("GnuPG tool did not start.");
    try {
      var stdout = process.StandardOutput.ReadToEndAsync(limit.Token);
      var stderr = process.StandardError.ReadToEndAsync(limit.Token);
      if (input is not null) {
        await process.StandardInput.WriteAsync(input.AsMemory(), limit.Token);
      }

      process.StandardInput.Close();
      await process.WaitForExitAsync(limit.Token);
      var result = await stdout;
      _ = await stderr;
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
