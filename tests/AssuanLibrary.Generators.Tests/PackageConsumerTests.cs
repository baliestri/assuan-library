// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace AssuanLibrary.Generators.Tests;

public sealed class PackageConsumerTests {
  private readonly static string Repository =
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

  private readonly static string PackageDirectory = Path.Combine(Repository, "artifacts", "packages");

  // The folder the solution itself was restored into. It serves as an offline feed for the consumers, so
  // they do not depend on whatever an older run left in the consumer cache under artifacts/.
  private static string RestoredPackages
    => Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
      ? configured
      : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

  [Fact]
  public async Task MainPackageContainsOneAnalyzerAndNoMandatoryMicrosoftDependencies() {
    Directory.CreateDirectory(PackageDirectory);
    await RunDotnetAsync(Repository, "pack", "src/AssuanLibrary/AssuanLibrary.csproj", "-c", "Release",
      "--no-restore", "-o", PackageDirectory);
    var package = Directory.EnumerateFiles(PackageDirectory, "AssuanLibrary.*.nupkg")
      .OrderByDescending(File.GetLastWriteTimeUtc).First();
    await using var archive = await ZipFile.OpenReadAsync(package, TestContext.Current.CancellationToken);
    Assert.Single(archive.Entries,
      item => string.Equals(item.FullName, "lib/net10.0/AssuanLibrary.dll", StringComparison.Ordinal));
    Assert.Single(archive.Entries, item => string.Equals(item.FullName, "README.md", StringComparison.Ordinal));
    Assert.Single(archive.Entries, item => string.Equals(item.FullName, "LICENSE.md", StringComparison.Ordinal));
    Assert.Single(archive.Entries,
      item => string.Equals(item.FullName, "analyzers/dotnet/cs/AssuanLibrary.Generators.dll",
        StringComparison.Ordinal));
    Assert.DoesNotContain(archive.Entries, item => item.FullName.StartsWith("lib/", StringComparison.Ordinal) &&
                                                   (item.Name.Contains("Generators", StringComparison.Ordinal) ||
                                                    item.Name.Contains("CodeAnalysis", StringComparison.Ordinal)));
    var nuspec = Assert.Single(archive.Entries, item => item.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
    await using var nuspecStream = await nuspec.OpenAsync(TestContext.Current.CancellationToken);
    var document = XDocument.Load(nuspecStream);
    var metadata = document.Root!.Element(document.Root.Name.Namespace + "metadata")!;
    var ns = metadata.Name.Namespace;
    Assert.Equal("Bruno Sales", metadata.Element(ns + "authors")?.Value);
    Assert.Contains("binary-safe", metadata.Element(ns + "description")?.Value, StringComparison.OrdinalIgnoreCase);
    Assert.Equal("README.md", metadata.Element(ns + "readme")?.Value);
    Assert.Equal("LICENSE.md", metadata.Element(ns + "license")?.Value);
    Assert.Equal("file", metadata.Element(ns + "license")?.Attribute("type")?.Value);
    Assert.Equal("https://github.com/baliestri/assuan-library", metadata.Element(ns + "projectUrl")?.Value);
    Assert.Equal("https://github.com/baliestri/assuan-library",
      metadata.Element(ns + "repository")?.Attribute("url")?.Value);
    Assert.Equal("git", metadata.Element(ns + "repository")?.Attribute("type")?.Value);
    Assert.Equal("assuan gpg gpgme gnupg", metadata.Element(ns + "tags")?.Value);
    var xml = document.ToString();
    Assert.DoesNotContain("Microsoft.Extensions", xml, StringComparison.Ordinal);
    Assert.DoesNotContain("AssuanLibrary.Generators", xml, StringComparison.Ordinal);
    Assert.Empty(Directory.EnumerateFiles(PackageDirectory, "AssuanLibrary.Generators.*.nupkg"));
  }

  [Fact]
  public async Task InstalledPackageGeneratesOnlyIntegrationsSupportedByConsumerReferences() {
    var suffix = Guid.NewGuid().ToString("N");
    var version = $"2.0.0-local.{suffix}";
    var packages = Path.Combine(PackageDirectory, suffix);
    Directory.CreateDirectory(packages);
    await RunDotnetAsync(Repository, "pack", "src/AssuanLibrary/AssuanLibrary.csproj", "-c", "Release",
      "--no-restore", "-o", packages, $"-p:PackageVersion={version}");
    foreach (var (di, logging) in new[] { (false, false), (true, false), (false, true), (true, true) }) {
      var consumer = Path.Combine(Repository, "artifacts", "integration-consumers", suffix,
        di ? logging ? "both" : "di" : logging ? "logging" : "none");
      Directory.CreateDirectory(consumer);
      await File.WriteAllTextAsync(Path.Combine(consumer, "Directory.Build.props"), """
      <Project><PropertyGroup>
        <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
        <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
      </PropertyGroup></Project>
      """, TestContext.Current.CancellationToken);
      await File.WriteAllTextAsync(Path.Combine(consumer, "Consumer.csproj"), $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
            <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
          </PropertyGroup>
          <ItemGroup>
        <PackageReference Include="AssuanLibrary" Version="{version}" />
        {(di ? Reference("Microsoft.Extensions.DependencyInjection") : "")}{(logging
          ? Reference("Microsoft.Extensions.Logging.Abstractions")
          : "")}</ItemGroup></Project>

        """,
        TestContext.Current.CancellationToken);
      await File.WriteAllTextAsync(Path.Combine(consumer, "Program.cs"), ConsumerSource(di, logging),
        TestContext.Current.CancellationToken);
      await RunDotnetAsync(consumer, "restore", "Consumer.csproj", "--source", packages, "--source",
        RestoredPackages);
      await RunDotnetAsync(consumer, "build", "Consumer.csproj", "-c", "Release", "--no-restore");
      await RunDotnetAsync(consumer, "run", "--project", "Consumer.csproj", "-c", "Release", "--no-build");
      var generated = Directory.GetFiles(Path.Combine(consumer, "obj"), "*.g.cs", SearchOption.AllDirectories)
        .Select(Path.GetFileName).ToArray();
      Assert.Contains(generated, name => name!.StartsWith("AssuanHandler_", StringComparison.Ordinal));
      Assert.Contains(generated, name => name!.StartsWith("AssuanGroup_", StringComparison.Ordinal));
      Assert.Equal(di,
        generated.Any(name => name!.StartsWith("AssuanDependencyInjectionExtensions", StringComparison.Ordinal)));
      Assert.Equal(logging,
        generated.Any(name => name!.StartsWith("AssuanLoggingExtensions", StringComparison.Ordinal)));
    }
  }

  // The consumers reference the centrally pinned versions, which are the ones the solution restore
  // downloaded; a hard-coded version here breaks as soon as Directory.Packages.props moves on.
  private static string Reference(string package) {
    var central = XDocument.Load(Path.Combine(Repository, "Directory.Packages.props"));
    // The file declares the MSBuild namespace, so elements are matched by local name.
    var version = central.Descendants()
      .Single(item => string.Equals(item.Name.LocalName, "PackageVersion", StringComparison.Ordinal) &&
                      string.Equals(item.Attribute("Include")?.Value, package, StringComparison.Ordinal))
      .Attribute("Version")!.Value;
    return $"<PackageReference Include=\"{package}\" Version=\"{version}\" />\n";
  }

  private static string ConsumerSource(bool di, bool logging)
    => $$"""
      using AssuanLibrary.Attributes;
      using AssuanLibrary.Server;
      [AssuanHandlerGroup]
      public static partial class Commands {
        [AssuanCommandHandler("ECHO", "Echo")]
        private static System.Threading.Tasks.ValueTask EchoAsync(
          CommandContext<object> context, System.Threading.CancellationToken token)
          => context.SendDataAsync(context.Arguments, token);
      }
      public static class Program {
        public static void Main() {
          var registry = new CommandRegistry<object>();
          Commands.RegisterHandlers(registry);
          if (registry.Find("ECHO") is null) throw new System.Exception("Missing handler");
      {{(di
        ? """
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            AssuanLibrary.Generated.AssuanDependencyInjectionExtensions.AddAssuanServer<object>(services,
              _ => new object(), ServerOptions.Default, new DefaultSessionHooks<object>());
        """
        : "")}}{{(logging
        ? """
            var sink = AssuanLibrary.Generated.AssuanLoggingExtensions.CreateEventSink(
              Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var info = new AssuanLibrary.Diagnostics.AssuanEvent(
              AssuanLibrary.Diagnostics.AssuanComponent.Client,
              AssuanLibrary.Diagnostics.AssuanEventId.RemoteFinal,
              AssuanLibrary.Diagnostics.AssuanOutcome.RemoteError);
            sink.Emit(in info);
        """
        : "")}}
        }
      }

      """;

  private static async Task RunDotnetAsync(string directory, params string[] arguments) {
    var start = new ProcessStartInfo("dotnet") {
      WorkingDirectory = directory,
      RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
    };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);

    start.Environment["NUGET_PACKAGES"] = Path.Combine(Repository, "artifacts", "nuget-cache");
    start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Repository, "artifacts", "dotnet-home");
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
    var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken)
      .WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken).ConfigureAwait(false);
    var result = await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false);
    Assert.True(process.ExitCode == 0, result);
  }
}
