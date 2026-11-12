// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AssuanLibrary.Tests.Release;

internal sealed partial class ReleaseFixture : IDisposable {
  private readonly string _parent;

  internal ReleaseFixture(bool versionProperty = true, bool patchRelease = false) {
    _parent = Path.Combine(ToolsRoot, "artifacts", "release-tests", Guid.NewGuid().ToString("N"));
    Root = Path.Combine(_parent, "candidate");
    var origin = Path.Combine(_parent, "origin.git");
    Directory.CreateDirectory(Root);
    Directory.CreateDirectory(Path.Combine(Root, "src", "AssuanLibrary"));
    Directory.CreateDirectory(Path.Combine(Root, "wiki"));
    File.WriteAllText(Path.Combine(Root, ".gitignore"), "artifacts/\n");
    File.WriteAllText(Path.Combine(Root, "CHANGELOG.md"), HistoricalChangelog);
    File.WriteAllText(Path.Combine(Root, "src", "AssuanLibrary", "AssuanLibrary.csproj"),
      $"<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework>{(versionProperty ? "<Version>1.2.0</Version>" : "")}</PropertyGroup></Project>\n");
    foreach (var path in new[] {
               "README.md", "src/AssuanLibrary/PACKAGE.md", "wiki/Getting-Started.md",
               "cliff.toml"
             })
      File.Copy(Path.Combine(ToolsRoot, path), Path.Combine(Root, path));

    var packageReadme = Path.Combine(Root, "src", "AssuanLibrary", "PACKAGE.md");
    File.WriteAllText(packageReadme, BaselinePackageReadme(File.ReadAllText(packageReadme)));

    Git("init", "-b", "main");
    Git("add", ".");
    Git("commit", "-m", "chore: retain historical release");
    Git("tag", "v1.2.0");
    Git("tag", "vv1.1.0");
    if (patchRelease) {
      File.WriteAllText(Path.Combine(Root, "src", "AssuanLibrary", "AssuanLibrary.csproj"),
        "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><Version>2.0.0</Version></PropertyGroup></Project>\n");
      File.WriteAllText(Path.Combine(Root, "CHANGELOG.md"),
        $"# Assuan Library .NET Changelog\n\n## [2.0.0] - published\n\nExisting release.\n\n{HistoricalChangelog["# Assuan Library .NET Changelog\n\n".Length..]}");
      Git("add", ".");
      Git("commit", "-m", "chore(release): publish 2.0.0");
      Git("tag", "v2.0.0");
    }

    var mainSha = Head;
    Git("switch", "-c", "develop");
    File.WriteAllText(Path.Combine(Root, "feature.txt"), patchRelease ? "Patch metadata.\n" : "Version two.\n");
    Git("add", "feature.txt");
    Git("commit", "-m", patchRelease ? "fix(package): restore metadata" : "feat!: replace the published 1.x API");
    GitIn(_parent, "init", "--bare", origin);
    Git("remote", "add", "origin", origin);
    Git("push", "origin", "main", "develop", "--tags"); // Only the disposable bare fixture under artifacts.
    Git("update-ref", "refs/remotes/origin/main", mainSha);
    Git("update-ref", "refs/remotes/origin/develop", Head);
  }

  internal string Root { get; }
  internal static string ToolsRoot { get; } = RepositoryRoot.Path;

  internal static string HistoricalChangelog
    => "# Assuan Library .NET Changelog\n\n## [1.2.0] - historical\n\nLegacy entry.\n";

  internal string Head => Git("rev-parse", "HEAD").Trim();

  [GeneratedRegex(@"dotnet add package AssuanLibrary --version (?<version>2\.[0-9]+\.[0-9]+)",
    RegexOptions.ExplicitCapture, 5000)]
  private static partial Regex VersionRegex { get; }

  public void Dispose() {
    foreach (var path in Directory.EnumerateFiles(_parent, "*", SearchOption.AllDirectories))
      File.SetAttributes(path, FileAttributes.Normal);

    Directory.Delete(_parent, true);
  }

  internal static string BaselinePackageReadme(string content) {
    var matches = VersionRegex.Matches(content);
    if (matches.Count != 1) {
      throw new InvalidDataException("The fixture requires exactly one package installation command.");
    }

    return content.Replace(matches[0].Value, "dotnet add package AssuanLibrary --version 2.0.0",
      StringComparison.Ordinal);
  }

  internal string Git(params string[] arguments)
    => GitIn(Root, arguments);

  internal async Task<(int ExitCode, string Output)> PrepareAsync(string version = "2.0.0", bool dryRun = true,
    params string[] extra) {
    var arguments = new List<string> {
      "-NoProfile", "-NonInteractive", "-File",
      Path.Combine(ToolsRoot, "scripts", "release", "Prepare-Version.ps1"), "-Root", Root,
      "-Version", version, "-ToolsRoot", ToolsRoot,
      dryRun ? "-DryRun" : "-Fixture"
    };
    arguments.AddRange(extra);
    return await RunAsync("pwsh", ToolsRoot, arguments.ToArray()).ConfigureAwait(false);
  }

  internal Task<(int ExitCode, string Output)> ResumeAsync(string sha, string version = "2.0.0")
    => RunAsync("pwsh", ToolsRoot, "-NoProfile", "-NonInteractive", "-File",
      Path.Combine(ToolsRoot, "scripts", "release", "Assert-ResumeCandidate.ps1"),
      "-Root", Root, "-Version", version, "-ResumeSha", sha);

  private static string GitIn(string directory, params string[] arguments) {
    var result = RunAsync("git", directory, arguments).GetAwaiter().GetResult();

    return result.ExitCode != 0
      ? throw new InvalidOperationException($"Disposable Git fixture failed: {result.Output}")
      : result.Output;
  }

  private static async Task<(int ExitCode, string Output)> RunAsync(string executable, string directory,
    params string[] arguments) {
    var start = new ProcessStartInfo(executable) {
      WorkingDirectory = directory, UseShellExecute = false,
      RedirectStandardOutput = true, RedirectStandardError = true
    };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);

    start.Environment["GIT_AUTHOR_NAME"] = "Fixture Author";
    start.Environment["GIT_AUTHOR_EMAIL"] = "fixture@example.invalid";
    start.Environment["GIT_COMMITTER_NAME"] = "Fixture Author";
    start.Environment["GIT_COMMITTER_EMAIL"] = "fixture@example.invalid";
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
    var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken)
      .WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken).ConfigureAwait(false);
    return (process.ExitCode, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
  }
}
