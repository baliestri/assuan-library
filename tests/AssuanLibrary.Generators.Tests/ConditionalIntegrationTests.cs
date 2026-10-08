// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssuanLibrary.Generators.Tests;

public sealed class ConditionalIntegrationTests {
  internal const string DI_TYPES = """
                                   namespace Microsoft.Extensions.DependencyInjection {
                                     public interface IServiceCollection { }
                                     public sealed class ServiceCollection : IServiceCollection { }
                                     public sealed class ServiceProvider : System.IServiceProvider {
                                       public object? GetService(System.Type serviceType) => null;
                                     }
                                     public static class ServiceCollectionServiceExtensions {
                                       public static IServiceCollection AddSingleton<T>(this IServiceCollection services,
                                         System.Func<System.IServiceProvider, T> factory) => services;
                                     }
                                   }
                                   """;

  internal const string LOGGING_TYPES = """
                                        namespace Microsoft.Extensions.Logging {
                                          public enum LogLevel { Information }
                                          public readonly struct EventId { public EventId(int id, string name) { } }
                                          public interface ILogger {
                                            void Log<T>(LogLevel level, EventId eventId, T state, System.Exception? exception,
                                              System.Func<T, System.Exception?, string> formatter);
                                          }
                                          public interface ILoggerFactory { ILogger CreateLogger(string category); }
                                        }
                                        """;

  [Fact]
  public void ReferencesAloneDetermineIndependentGenerationFlags() {
    Assert.Equal((false, false), Detect("public class Empty { }"));
    Assert.Equal((true, false), Detect(DI_TYPES));
    Assert.Equal((false, true), Detect(LOGGING_TYPES));
    Assert.Equal((true, true), Detect(DI_TYPES + "\n" + LOGGING_TYPES));
  }

  [Fact]
  public void UnrelatedConsumerCodeCannotProduceIntegrationWithoutRequiredSymbols() {
    var result = GeneratorHarness.Compile("public class Unrelated { }");
    Assert.Empty(result.GeneratedSources);
    Assert.Empty(result.GeneratorDiagnostics);
  }

  [Fact]
  public void DependencyInjectionReferenceGeneratesOnlyACompilableDiHelper() {
    var result = GeneratorHarness.Compile(DI_TYPES + "\n" + """
                                                            public static class Consumer {
                                                              public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services,
                                                                AssuanLibrary.Server.ISessionHooks<object> hooks) {
                                                                AssuanLibrary.Generated.AssuanDependencyInjectionExtensions.AddAssuanServer<object>(
                                                                  services, _ => new object(), AssuanLibrary.Server.ServerOptions.Default, hooks);
                                                                AssuanLibrary.Generated.AssuanDependencyInjectionExtensions.AddAssuanClientFactory(
                                                                  services, AssuanLibrary.Client.ClientOptions.Default);
                                                              }
                                                            }
                                                            """);
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    var text = Assert.Single(result.GeneratedSources);
    Assert.Contains("AddAssuanServer", text);
    Assert.Contains("AddAssuanClientFactory", text);
    Assert.DoesNotContain("ILogger", text);
    Assert.Single(result.GeneratedHints);
  }

  [Fact]
  public void ExistingFixedGeneratedClassNameGetsCollisionDiagnosticInsteadOfDuplicateSource() {
    var result = GeneratorHarness.Compile(DI_TYPES + "\n" + """
                                                            namespace AssuanLibrary.Generated {
                                                              public static class AssuanDependencyInjectionExtensions { }
                                                            }
                                                            """);
    Assert.Contains(result.GeneratorDiagnostics, item => item.Id == "ASG011");
    Assert.Empty(result.GeneratedSources);
  }

  [Fact]
  public void RealDiReferencesCompileExtensionsWithoutLogging() {
    const string SOURCE = """
                          using AssuanLibrary.Generated;
                          using Microsoft.Extensions.DependencyInjection;
                          public static class Application {
                            public static void Register(IServiceCollection services) {
                              services.AddAssuanServer<object>(_ => new object(), AssuanLibrary.Server.ServerOptions.Default,
                                new AssuanLibrary.Server.DefaultSessionHooks<object>());
                              services.AddAssuanClientFactory(AssuanLibrary.Client.ClientOptions.Default);
                            }
                          }
                          """;
    var result = GeneratorHarness.Compile(SOURCE, extraReferences: [
      MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
      MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location)
    ]);
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    var item = Assert.Single(result.GeneratedSources);
    Assert.DoesNotContain("ILogger", item);
  }

  [Fact]
  public void RealLoggingReferencesGenerateOnlyOneLoggingAdapter() {
    var result = GeneratorHarness.Compile("""
                                          public static class Consumer {
                                            public static AssuanLibrary.Diagnostics.IAssuanEventSink Create(
                                              Microsoft.Extensions.Logging.ILogger logger) =>
                                              AssuanLibrary.Generated.AssuanLoggingExtensions.CreateEventSink(logger);
                                          }
                                          """, extraReferences: [
      MetadataReference.CreateFromFile(
        typeof(ILogger).Assembly.Location)
    ]);
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    var text = Assert.Single(result.GeneratedSources);
    Assert.Contains("CreateEventSink", text);
    Assert.Contains("ILogger", text);
    Assert.DoesNotContain("IServiceCollection", text);
  }

  [Fact]
  public void BothReferenceSetsProduceTwoUniqueNonOverlappingSources() {
    var result = GeneratorHarness.Compile("public sealed class Marker {}", extraReferences: [
      MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
      MetadataReference.CreateFromFile(typeof(ServiceProvider).Assembly.Location),
      MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location)
    ]);
    Assert.Empty(result.GeneratorDiagnostics);
    Assert.Empty(result.OutputCompilationErrors);
    Assert.Equal(2, result.GeneratedSources.Length);
    Assert.Equal(2, result.GeneratedHints.Distinct().Count());
    Assert.Equal(1, result.GeneratedSources.Count(source => source.Contains("AddAssuanServer")));
    Assert.Equal(1, result.GeneratedSources.Count(source => source.Contains("CreateEventSink")));
  }

  private static (bool Di, bool Logging) Detect(string source) {
    var result = GeneratorHarness.Compile(source, false);
    Assert.Empty(result.OutputCompilationErrors);
    var found = IntegrationAvailability.Detect(result.OutputCompilation);
    return (found.Di, found.Logging);
  }
}
