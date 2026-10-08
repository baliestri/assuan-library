// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace AssuanLibrary.Generators.Tests;

public sealed class PackageConsumerTests {
  private static readonly string _repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
  private static readonly string _packageDirectory = Path.Combine(_repository, "artifacts", "packages");

  [Fact]
  public async Task MainPackageContainsOneAnalyzerAndNoMandatoryMicrosoftDependencies() {
    Directory.CreateDirectory(_packageDirectory);
    await RunDotnetAsync(_repository, "pack", "src/AssuanLibrary/AssuanLibrary.csproj", "-c", "Release",
      "--no-restore", "-o", _packageDirectory);
    var package = Directory.EnumerateFiles(_packageDirectory, "AssuanLibrary.*.nupkg")
      .OrderByDescending(File.GetLastWriteTimeUtc).First();
    await using var archive = ZipFile.OpenRead(package);
    Assert.Single(archive.Entries, item => item.FullName == "lib/net10.0/AssuanLibrary.dll");
    Assert.Single(archive.Entries, item => item.FullName ==
                                           "analyzers/dotnet/cs/AssuanLibrary.Generators.dll");
    Assert.DoesNotContain(archive.Entries, item => item.FullName.StartsWith("lib/", StringComparison.Ordinal) &&
                                                   (item.Name.Contains("Generators", StringComparison.Ordinal) ||
                                                    item.Name.Contains("CodeAnalysis", StringComparison.Ordinal)));
    var nuspec = Assert.Single(archive.Entries, item => item.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
    await using var nuspecStream = nuspec.Open();
    var xml = XDocument.Load(nuspecStream).ToString();
    Assert.DoesNotContain("Microsoft.Extensions", xml);
    Assert.DoesNotContain("AssuanLibrary.Generators", xml);
    Assert.Empty(Directory.EnumerateFiles(_packageDirectory, "AssuanLibrary.Generators.*.nupkg"));
  }

  [Fact]
  public async Task InstalledPackageGeneratesOnlyIntegrationsSupportedByConsumerReferences() {
    var suffix = Guid.NewGuid().ToString("N");
    var version = "2.0.0-local." + suffix;
    var packages = Path.Combine(_packageDirectory, suffix);
    Directory.CreateDirectory(packages);
    await RunDotnetAsync(_repository, "pack", "src/AssuanLibrary/AssuanLibrary.csproj", "-c", "Release",
      "--no-restore", "-o", packages, "-p:PackageVersion=" + version);
    foreach (var (di, logging) in new[] { (false, false), (true, false), (false, true), (true, true) }) {
      var consumer = Path.Combine(_repository, "artifacts", "integration-consumers", suffix,
        di ? logging ? "both" : "di" : logging ? "logging" : "none");
      Directory.CreateDirectory(consumer);
      File.WriteAllText(Path.Combine(consumer, "Directory.Build.props"), """
                                                                         <Project><PropertyGroup>
                                                                           <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
                                                                           <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
                                                                           <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                                                                         </PropertyGroup></Project>
                                                                         """);
      File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), """
                                                                   <Project Sdk="Microsoft.NET.Sdk">
                                                                     <PropertyGroup>
                                                                       <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
                                                                       <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                                                                     </PropertyGroup>
                                                                     <ItemGroup>
                                                                   """ + "\n" +
                                                                   "<PackageReference Include=\"AssuanLibrary\" Version=\"" + version + "\" />\n" +
                                                                   (di
                                                                     ? "<PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" Version=\"10.0.0\" />\n"
                                                                     : "") +
                                                                   (logging
                                                                     ? "<PackageReference Include=\"Microsoft.Extensions.Logging.Abstractions\" Version=\"10.0.0\" />\n"
                                                                     : "") +
                                                                   "</ItemGroup></Project>\n");
      File.WriteAllText(Path.Combine(consumer, "Program.cs"), ConsumerSource(di, logging));
      await RunDotnetAsync(consumer, "restore", "Consumer.csproj", "--source", packages);
      await RunDotnetAsync(consumer, "build", "Consumer.csproj", "-c", "Release", "--no-restore");
      await RunDotnetAsync(consumer, "run", "--project", "Consumer.csproj", "-c", "Release", "--no-build");
      var generated = Directory.GetFiles(Path.Combine(consumer, "obj"), "*.g.cs", SearchOption.AllDirectories)
        .Select(Path.GetFileName).ToArray();
      Assert.Contains(generated, name => name!.StartsWith("AssuanHandler_", StringComparison.Ordinal));
      Assert.Contains(generated, name => name!.StartsWith("AssuanGroup_", StringComparison.Ordinal));
      Assert.Equal(di, generated.Any(name => name!.StartsWith("AssuanDependencyInjectionExtensions", StringComparison.Ordinal)));
      Assert.Equal(logging, generated.Any(name => name!.StartsWith("AssuanLoggingExtensions", StringComparison.Ordinal)));
    }
  }

  private static string ConsumerSource(bool di, bool logging)
    => """
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
       """ + "\n" + (di
      ? """
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            AssuanLibrary.Generated.AssuanDependencyInjectionExtensions.AddAssuanServer<object>(services,
              _ => new object(), ServerOptions.Default, new DefaultSessionHooks<object>());
        """
      : "") + (logging
      ? """
            var sink = AssuanLibrary.Generated.AssuanLoggingExtensions.CreateEventSink(
              Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var info = new AssuanLibrary.Diagnostics.AssuanEvent(
              AssuanLibrary.Diagnostics.AssuanComponent.Client,
              AssuanLibrary.Diagnostics.AssuanEventId.RemoteFinal,
              AssuanLibrary.Diagnostics.AssuanOutcome.RemoteError);
            sink.Emit(in info);
        """
      : "") + "\n  }\n}\n";

  private static async Task RunDotnetAsync(string directory, params string[] arguments) {
    var start = new ProcessStartInfo("dotnet") {
      WorkingDirectory = directory,
      RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
    };
    foreach (var argument in arguments) {
      start.ArgumentList.Add(argument);
    }

    start.Environment["NUGET_PACKAGES"] = Path.Combine(_repository, "artifacts", "nuget-cache");
    start.Environment["DOTNET_CLI_HOME"] = Path.Combine(_repository, "artifacts", "dotnet-home");
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
    var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken)
      .WaitAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
    var result = await stdout + await stderr;
    Assert.True(process.ExitCode == 0, result);
  }
}
