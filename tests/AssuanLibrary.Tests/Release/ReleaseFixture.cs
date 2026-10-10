// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using AssuanLibrary.Docs;

namespace AssuanLibrary.Tests.Release;

internal sealed class ReleaseFixture : IDisposable {
  private readonly string _parent;

  internal ReleaseFixture(bool versionProperty = true) {
    _parent = Path.Combine(ToolsRoot, "artifacts", "release-tests", Guid.NewGuid().ToString("N"));
    Root = Path.Combine(_parent, "candidate");
    var origin = Path.Combine(_parent, "origin.git");
    Directory.CreateDirectory(Root);
    Directory.CreateDirectory(Path.Combine(Root, "src", "AssuanLibrary"));
    Directory.CreateDirectory(Path.Combine(Root, "docs", "nuget"));
    Directory.CreateDirectory(Path.Combine(Root, "docs", "guides"));
    File.WriteAllText(Path.Combine(Root, ".gitignore"), "artifacts/\n");
    File.WriteAllText(Path.Combine(Root, "CHANGELOG.md"), HistoricalChangelog);
    File.WriteAllText(Path.Combine(Root, "src", "AssuanLibrary", "AssuanLibrary.csproj"),
      "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework>" +
      (versionProperty ? "<Version>1.2.0</Version>" : "") + "</PropertyGroup></Project>\n");
    foreach (var path in new[] {
               "README.adoc", "docs/nuget/README.md", "docs/guides/getting-started.md",
               "cliff.toml"
             }) {
      File.Copy(Path.Combine(ToolsRoot, path), Path.Combine(Root, path));
    }

    Git("init", "-b", "main");
    Git("add", ".");
    Git("commit", "-m", "chore: retain historical release");
    Git("tag", "v1.2.0");
    Git("tag", "vv1.1.0");
    var mainSha = Head;
    Git("switch", "-c", "develop");
    File.WriteAllText(Path.Combine(Root, "feature.txt"), "Version two.\n");
    Git("add", "feature.txt");
    Git("commit", "-m", "feat!: replace the published 1.x API");
    GitIn(_parent, "init", "--bare", origin);
    Git("remote", "add", "origin", origin);
    Git("push", "origin", "main", "develop", "--tags"); // Only the disposable bare fixture under artifacts.
    Git("update-ref", "refs/remotes/origin/main", mainSha);
    Git("update-ref", "refs/remotes/origin/develop", Head);
  }

  internal string Root { get; }
  internal static string ToolsRoot { get; } = DocumentationBuilder.FindRepositoryRoot(AppContext.BaseDirectory);

  internal static string HistoricalChangelog => "# Assuan Library .NET Changelog\n\n## [1.2.0] - historical\n\nLegacy entry.\n";
  internal string Head => Git("rev-parse", "HEAD").Trim();

  public void Dispose() {
    foreach (var path in Directory.EnumerateFiles(_parent, "*", SearchOption.AllDirectories)) {
      File.SetAttributes(path, FileAttributes.Normal);
    }

    Directory.Delete(_parent, true);
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
    return await RunAsync("pwsh", ToolsRoot, arguments.ToArray());
  }

  internal Task<(int ExitCode, string Output)> ResumeAsync(string sha)
    => RunAsync("pwsh", ToolsRoot, "-NoProfile", "-NonInteractive", "-File",
      Path.Combine(ToolsRoot, "scripts", "release", "Assert-ResumeCandidate.ps1"),
      "-Root", Root, "-Version", "2.0.0", "-ResumeSha", sha);

  private static string GitIn(string directory, params string[] arguments) {
    var result = RunAsync("git", directory, arguments).GetAwaiter().GetResult();
    if (result.ExitCode != 0) {
      throw new InvalidOperationException("Disposable Git fixture failed: " + result.Output);
    }

    return result.Output;
  }

  private static async Task<(int ExitCode, string Output)> RunAsync(string executable, string directory,
  params string[] arguments) {
    var start = new ProcessStartInfo(executable) {
      WorkingDirectory = directory, UseShellExecute = false,
      RedirectStandardOutput = true, RedirectStandardError = true
    };
    foreach (var argument in arguments) {
      start.ArgumentList.Add(argument);
    }

    start.Environment["GIT_AUTHOR_NAME"] = "Fixture Author";
    start.Environment["GIT_AUTHOR_EMAIL"] = "fixture@example.invalid";
    start.Environment["GIT_COMMITTER_NAME"] = "Fixture Author";
    start.Environment["GIT_COMMITTER_EMAIL"] = "fixture@example.invalid";
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
    return (process.ExitCode, await stdout + await stderr);
  }
}
