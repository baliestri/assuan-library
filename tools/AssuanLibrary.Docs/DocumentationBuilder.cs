// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssuanLibrary.Docs;

/// <summary>One reviewed source in the versioned documentation manifest.</summary>
public sealed record DocSource(int Order,
  string Title,
  string Source,
  string Destination,
  bool IncludeInIndex,
  bool IncludeInFull,
  bool Optional);

/// <summary>Validates sources and creates an immutable-by-policy local documentation version.</summary>
public static partial class DocumentationBuilder {
  private const string PUBLISHED_PREFIX = "https://baliestri.github.io/assuan-library/2.0.0/";

  private static readonly JsonSerializerOptions _jsonOptions = new() {
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
  };

  /// <summary>Finds the checkout from a build output, including RID-specific outputs.</summary>
  public static string FindRepositoryRoot(string directory) {
    for (var current = new DirectoryInfo(Path.GetFullPath(directory));
         current is not null;
         current = current.Parent) {
      if (File.Exists(Path.Combine(current.FullName, "AssuanLibrary.slnx")) &&
          File.Exists(Path.Combine(current.FullName, "docs", "site", "sources.json"))) {
        return current.FullName;
      }
    }

    throw new DirectoryNotFoundException("AssuanLibrary checkout was not found.");
  }

  /// <summary>Reads and validates relative paths, order and curated full-text selection.</summary>
  public static IReadOnlyList<DocSource> ReadManifest(string repository, string manifestPath) {
    var root = Path.GetFullPath(repository);
    var path = Inside(root, manifestPath);
    using var json = JsonDocument.Parse(File.ReadAllText(path));
    if (!json.RootElement.TryGetProperty("sources", out var entries) ||
        entries.ValueKind != JsonValueKind.Array) {
      throw new InvalidDataException("Manifest sources missing.");
    }

    var sources = new List<DocSource>();
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var previous = 0;
    foreach (var entry in entries.EnumerateArray()) {
      DocSource? source;
      try { source = entry.Deserialize<DocSource>(_jsonOptions); }
      catch (JsonException) { throw new InvalidDataException("Invalid manifest entry."); }

      if (source is null ||
          source.Order <= previous ||
          string.IsNullOrWhiteSpace(source.Title) ||
          !names.Add(source.Source) ||
          !destinations.Add(source.Destination) ||
          source is { IncludeInFull: true, IncludeInIndex: false }) {
        throw new InvalidDataException("Duplicate or inconsistent manifest entry.");
      }

      previous = source.Order;
      ValidateRelative(source.Source);
      ValidateRelative(source.Destination);
      if (!source.Destination.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidDataException("Destination is not Markdown.");
      }

      var original = Inside(root, Path.Combine(root, source.Source));
      if (!File.Exists(original) &&
          !source.Optional) {
        throw new InvalidDataException("Required source missing.");
      }

      sources.Add(source);
    }

    if (sources.Count == 0) {
      throw new InvalidDataException("Manifest is empty.");
    }

    return sources;
  }

  /// <summary>Builds metadata, HTML, curated Markdown and llms files without overwriting a version.</summary>
  public static async Task<string> BuildAsync(string repository, string version, string baseUrl,
  string outputRoot, CancellationToken token) {
    var root = Path.GetFullPath(repository);
    if (!VersionRegex().IsMatch(version) ||
        !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
        uri.Scheme is not ("http" or "https")) {
      throw new ArgumentException("Invalid version or base URL.");
    }

    var manifest = ReadManifest(root, Path.Combine(root, "docs", "site", "sources.json"));
    var output = Inside(root, Path.IsPathRooted(outputRoot) ? outputRoot : Path.Combine(root, outputRoot));
    var versionRoot = Inside(root, Path.Combine(output, version));
    if (Directory.Exists(versionRoot) ||
        File.Exists(versionRoot)) {
      throw new IOException("Version output already exists.");
    }

    var baseLink = baseUrl.TrimEnd('/') + "/" + version + "/";
    var stage = Path.Combine(versionRoot, "_stage");
    Directory.CreateDirectory(stage);
    foreach (var source in manifest) {
      var origin = Path.Combine(root, source.Source);
      if (!File.Exists(origin)) {
        continue;
      }

      var destination = Path.Combine(stage, source.Destination);
      Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
      var markdown = RewriteUrls(File.ReadAllText(origin), baseLink, version);
      await File.WriteAllTextAsync(destination, markdown, new UTF8Encoding(false), token);
    }

    await RunDotnet(root, token, "tool", "restore");
    await RunDotnet(root, token, "tool", "run", "docfx", "--", "metadata",
      Path.Combine(root, "docs", "site", "docfx.json"), "--output", stage, "--noRestore", "--disableGitFeatures");
    var tempConfig = Path.Combine(stage, "docfx.json");
    var config = new {
      build = new {
        content = new[] { new { files = new[] { "api/**.yml", "api/toc.yml", "guides/**.md", "index.md" } } },
        dest = "../site",
        template = new[] { "default" },
        globalMetadata = new { _appTitle = "AssuanLibrary " + version }
      }
    };
    await File.WriteAllTextAsync(tempConfig, JsonSerializer.Serialize(config, _jsonOptions), token);
    await RunDotnet(root, token, "tool", "run", "docfx", "--", "build", tempConfig, "--disableGitFeatures");
    // The default DocFX theme links to #top without defining that anchor.
    // Give its back-to-top links a real target before validating the site.
    foreach (var page in Directory.EnumerateFiles(Path.Combine(versionRoot, "site"), "*.html",
               SearchOption.AllDirectories)) {
      var html = await File.ReadAllTextAsync(page, token);
      if (html.Contains("href=\"#top\"", StringComparison.Ordinal) &&
          !html.Contains("id=\"top\"", StringComparison.Ordinal)) {
        html = html.Replace("<body ", "<body id=\"top\" ", StringComparison.Ordinal);
        await File.WriteAllTextAsync(page, html, new UTF8Encoding(false), token);
      }
    }

    // DocFX embeds its absolute staging path in the site manifest. It is not
    // needed by static pages and would make equivalent output roots differ.
    var siteManifest = Path.Combine(versionRoot, "site", "manifest.json");
    if (File.Exists(siteManifest)) {
      var manifestText = await File.ReadAllTextAsync(siteManifest, token);
      await File.WriteAllTextAsync(siteManifest, manifestText.Replace(
          stage.Replace('\\', '/'), "_stage", StringComparison.OrdinalIgnoreCase),
        new UTF8Encoding(false), token);
    }

    var index = new StringBuilder("# AssuanLibrary " + version + "\n\n");
    index.AppendLine("> .NET 10 Assuan library: reviewed guides and generated API reference.").AppendLine();
    index.AppendLine("## Guides");
    foreach (var source in manifest.Where(item => item.IncludeInIndex)) {
      index.Append("- [").Append(source.Title).Append("](").Append(baseLink)
        .Append(source.Destination[..^3]).Append(".html): ")
        .Append(source.Title).AppendLine(" for .NET consumers.");
    }

    index.Append("\n- [Public API reference](").Append(baseLink).AppendLine("api/toc.html): exported types and members.");
    index.Append("- [Selectable examples](https://github.com/baliestri/assuan-library/tree/v")
      .Append(version).AppendLine("/examples/AssuanLibrary.Examples): compiled .NET scenarios.");
    var full = new StringBuilder("# AssuanLibrary " + version + " — user guides\n\n");
    full.Append("Public API reference: ").Append(baseLink).AppendLine("api/toc.html\n");
    foreach (var source in manifest.Where(item => item.IncludeInFull)) {
      full.Append("\n---\n\n## ").Append(source.Title).Append("\n\nSource: ")
        .Append(baseLink).Append(source.Destination[..^3]).AppendLine(".html\n");
      full.AppendLine(await File.ReadAllTextAsync(Path.Combine(stage, source.Destination), token));
    }

    foreach (var (name, content) in new[] {
               ("llms.txt", index.ToString()),
               ("llms-full.txt", full.ToString())
             }) {
      await File.WriteAllTextAsync(Path.Combine(versionRoot, name), content, new UTF8Encoding(false), token);
      await File.WriteAllTextAsync(Path.Combine(versionRoot, "site", name), content, new UTF8Encoding(false), token);
    }

    await File.WriteAllTextAsync(Path.Combine(versionRoot, "metadata.json"),
      JsonSerializer.Serialize(new { version, baseUrl = baseLink, sourceCount = manifest.Count }, _jsonOptions), token);
    var errors = Verify(versionRoot);
    if (errors.Count != 0) {
      throw new InvalidDataException("Documentation verification failed: " +
                                     string.Join("; ", errors.Distinct().Take(20)));
    }

    return versionRoot;
  }

  /// <summary>Checks the generated public API, selected guides and versioned llms links.</summary>
  public static IReadOnlyList<string> Verify(string bundlePath) {
    var root = Path.GetFullPath(bundlePath);
    var errors = new List<string>();
    if (!Directory.Exists(root)) {
      return ["Bundle directory is absent."];
    }

    foreach (var relative in new[] {
               "llms.txt", "llms-full.txt", "metadata.json", "site/index.html",
               "site/api/AssuanLibrary.Client.AssuanClient.html",
               "site/api/AssuanLibrary.Server.ServerSession-1.html"
             }) {
      if (!File.Exists(Path.Combine(root, relative))) {
        errors.Add("Missing required output: " + relative);
      }
    }

    if (File.Exists(Path.Combine(root, "site/api/AssuanLibrary.Transport.UnixNative.html"))) {
      errors.Add("An internal API was published.");
    }

    foreach (var asset in new[] {
               "site/styles/docfx.css", "site/styles/main.css",
               "site/styles/docfx.js"
             }) {
      if (!File.Exists(Path.Combine(root, asset))) {
        errors.Add("Missing site asset: " + asset);
      }
    }

    var metadataFile = Path.Combine(root, "metadata.json");
    if (File.Exists(metadataFile)) {
      using var metadata = JsonDocument.Parse(File.ReadAllText(metadataFile));
      var baseLink = metadata.RootElement.GetProperty("baseUrl").GetString() ?? "";
      foreach (var textFile in new[] { "llms.txt", "llms-full.txt" }) {
        var filename = Path.Combine(root, textFile);
        if (!File.Exists(filename)) {
          continue;
        }

        foreach (Match match in Regex.Matches(File.ReadAllText(filename), @"https?://[^\s)]+")) {
          var url = match.Value;
          if (!url.StartsWith(baseLink, StringComparison.Ordinal)) {
            continue;
          }

          var relative = url[baseLink.Length..].TrimEnd('.', ',', ';');
          if (relative.Contains('#')) {
            relative = relative[..relative.IndexOf('#')];
          }

          var target = relative.Length == 0 ? "index.html" : relative;
          if (!File.Exists(Path.Combine(root, "site", target.Replace('/', Path.DirectorySeparatorChar)))) {
            errors.Add("Broken llms link: " + relative);
          }
        }
      }
    }

    var stage = Path.Combine(root, "_stage");
    if (Directory.Exists(stage)) {
      foreach (var source in Directory.EnumerateFiles(stage, "*.md", SearchOption.AllDirectories)) {
        foreach (Match match in MarkdownUrlRegex().Matches(File.ReadAllText(source))) {
          var href = match.Groups[1].Value;
          if (Uri.TryCreate(href, UriKind.Absolute, out var external)) {
            if (external.Scheme is not ("http" or "https")) {
              errors.Add("Unsupported link scheme.");
            }

            continue;
          }

          if (href.StartsWith("xref:", StringComparison.OrdinalIgnoreCase)) {
            continue;
          }

          var parts = href.Split('#', 2);
          var linked = parts[0].Length == 0
            ? source
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,
              Uri.UnescapeDataString(parts[0].Replace('/', Path.DirectorySeparatorChar))));
          if (!linked.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
              !string.Equals(linked, source, StringComparison.OrdinalIgnoreCase)) {
            errors.Add("Broken local link: outside bundle.");
            continue;
          }

          if (!File.Exists(linked)) {
            errors.Add("Broken local link: " + Path.GetFileName(linked));
            continue;
          }

          var html = Path.Combine(root, "site", Path.GetRelativePath(stage, linked));
          html = Path.ChangeExtension(html, ".html");
          if (!File.Exists(html)) {
            errors.Add("Broken generated page: " + Path.GetFileName(html));
            continue;
          }

          if (parts.Length == 2 &&
              parts[1].Length != 0 &&
              !File.ReadAllText(html).Contains("id=\"" + parts[1] + "\"", StringComparison.OrdinalIgnoreCase)) {
            errors.Add("Broken anchor: " + parts[1]);
          }
        }
      }
    }

    var site = Path.Combine(root, "site");
    if (Directory.Exists(site)) {
      foreach (var page in Directory.EnumerateFiles(site, "*.html", SearchOption.AllDirectories)) {
        var html = File.ReadAllText(page);
        foreach (Match match in HtmlUrlRegex().Matches(html)) {
          var href = WebUtility.HtmlDecode(match.Groups[1].Value);
          if (Uri.TryCreate(href, UriKind.Absolute, out var external)) {
            if (external.Scheme is not ("http" or "https" or "mailto" or "data")) {
              errors.Add("Unsupported site link scheme: " + Path.GetFileName(page));
            }

            continue;
          }

          if (href.StartsWith("//", StringComparison.Ordinal)) {
            continue;
          }

          var parts = href.Split('#', 2);
          var pathPart = parts[0].Split('?', 2)[0];
          var target = pathPart.Length == 0
            ? page
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(page)!,
              Uri.UnescapeDataString(pathPart.Replace('/', Path.DirectorySeparatorChar))));
          if (!target.StartsWith(site + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            errors.Add("Broken site link: outside bundle in " + Path.GetFileName(page));
            continue;
          }

          if (Directory.Exists(target)) {
            target = Path.Combine(target, "index.html");
          }

          if (!File.Exists(target)) {
            errors.Add("Broken site link: " + href);
            continue;
          }

          if (parts.Length == 2 &&
              parts[1].Length != 0) {
            var anchor = Uri.UnescapeDataString(parts[1]);
            if (!File.ReadAllText(target).Contains("id=\"" + anchor + "\"", StringComparison.OrdinalIgnoreCase)) {
              errors.Add("Broken site anchor: " + href);
            }
          }
        }
      }
    }

    return errors;
  }

  private static string RewriteUrls(string text, string baseLink, string version) =>
    text.Replace(PUBLISHED_PREFIX, baseLink, StringComparison.Ordinal)
      .Replace("https://github.com/baliestri/assuan-library/tree/v2.0.0/",
        "https://github.com/baliestri/assuan-library/tree/v" + version + "/", StringComparison.Ordinal);

  private static void ValidateRelative(string path) {
    if (string.IsNullOrWhiteSpace(path) ||
        Path.IsPathRooted(path) ||
        path.Contains(':') ||
        path.Split('/', '\\').Any(segment => segment is "" or "." or "..")) {
      throw new InvalidDataException("Invalid relative manifest path.");
    }
  }

  private static string Inside(string root, string path) {
    var full = Path.GetFullPath(path);
    var relative = Path.GetRelativePath(root, full);
    if (relative is ".." ||
        relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        Path.IsPathRooted(relative)) {
      throw new InvalidDataException("Path escapes repository.");
    }

    return full;
  }

  private static async Task RunDotnet(string root, CancellationToken token, params string[] args) {
    var start = new ProcessStartInfo("dotnet") {
      WorkingDirectory = root,
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    foreach (var argument in args) {
      start.ArgumentList.Add(argument);
    }

    var artifacts = Path.Combine(root, "artifacts");
    start.Environment["NUGET_PACKAGES"] = Path.Combine(artifacts, "nuget-cache");
    var configuredHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
    var localHome = string.IsNullOrWhiteSpace(configuredHome)
      ? ".."
      : Path.GetRelativePath(artifacts,
        Path.GetFullPath(configuredHome));
    start.Environment["DOTNET_CLI_HOME"] = localHome is ".." or "." ||
                                           localHome.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                                           Path.IsPathRooted(localHome)
      ? Path.Combine(artifacts, OperatingSystem.IsWindows() ? "dotnet-home" : "dotnet-home-linux")
      : Path.GetFullPath(configuredHome!);
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    using var process = Process.Start(start) ?? throw new IOException("Documentation tool did not start.");
    var stdout = process.StandardOutput.ReadToEndAsync(token);
    var stderr = process.StandardError.ReadToEndAsync(token);
    await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromMinutes(3), token);
    var report = await stdout + await stderr;
    if (process.ExitCode != 0 ||
        ReportRegex().IsMatch(report)) {
      throw new InvalidDataException("Documentation tool returned an error or warning (" +
                                     string.Join(' ', args.Take(2)) + "): " + report[^Math.Min(report.Length, 1200)..]);
    }
  }

  [GeneratedRegex("^[0-9][A-Za-z0-9.+-]*$", RegexOptions.CultureInvariant)]
  private static partial Regex VersionRegex();

  [GeneratedRegex(@"(?<!!)\[[^\]]+\]\(([^)]+)\)")]
  private static partial Regex MarkdownUrlRegex();

  [GeneratedRegex("(?:href|src)=[\"']([^\"']+)[\"']")]
  private static partial Regex HtmlUrlRegex();

  [GeneratedRegex(@"(?<!\d)[1-9]\d* warning\(s\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
  private static partial Regex ReportRegex();
}
