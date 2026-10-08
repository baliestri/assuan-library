// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AssuanLibrary.Generators.Models;

// Value-only compilation feature detection; no Roslyn symbol survives this stage.
internal readonly struct IntegrationAvailability : IEquatable<IntegrationAvailability> {
  internal bool Di { get; }
  internal bool Logging { get; }
  internal bool DiCollision { get; }
  internal bool LoggingCollision { get; }

  private IntegrationAvailability(bool di, bool logging, bool diCollision, bool loggingCollision) {
    Di = di;
    Logging = logging;
    DiCollision = diCollision;
    LoggingCollision = loggingCollision;
  }

  internal static IntegrationAvailability Detect(Compilation compilation) {
    var services = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");
    var concreteServices = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceCollection");
    var provider = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceProvider");
    var methods = compilation.GetTypeByMetadataName(
      "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions");
    var di = services is { TypeKind: TypeKind.Interface, DeclaredAccessibility: Accessibility.Public } &&
             concreteServices is { TypeKind: TypeKind.Class, DeclaredAccessibility: Accessibility.Public } &&
             concreteServices.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, services)) &&
             provider is { TypeKind: TypeKind.Class, DeclaredAccessibility: Accessibility.Public } &&
             methods is { DeclaredAccessibility: Accessibility.Public } &&
             methods.GetMembers("AddSingleton").OfType<IMethodSymbol>().Any(method =>
               method.IsStatic && method is { IsExtensionMethod: true, IsGenericMethod: true, TypeParameters.Length: 1, Parameters.Length: 2 } &&
               SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, services));

    var logger = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger");
    var factory = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILoggerFactory");
    var logging = logger is { TypeKind: TypeKind.Interface, DeclaredAccessibility: Accessibility.Public } &&
                  factory is { TypeKind: TypeKind.Interface, DeclaredAccessibility: Accessibility.Public } &&
                  logger.GetMembers("Log").OfType<IMethodSymbol>().Any(method => method.IsGenericMethod &&
                                                                                 method.Parameters.Length == 5) &&
                  factory.GetMembers("CreateLogger").OfType<IMethodSymbol>().Any(method =>
                    method.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(method.ReturnType, logger));
    return new IntegrationAvailability(di, logging,
      InConsumer(compilation, "AssuanLibrary.Generated.AssuanDependencyInjectionExtensions"),
      InConsumer(compilation, "AssuanLibrary.Generated.AssuanLoggingExtensions"));
  }

  private static bool InConsumer(Compilation compilation, string metadataName) {
    var type = compilation.GetTypeByMetadataName(metadataName);
    return type is not null && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);
  }

  public bool Equals(IntegrationAvailability other)
    => Di == other.Di && Logging == other.Logging &&
       DiCollision == other.DiCollision && LoggingCollision == other.LoggingCollision;

  public override bool Equals(object? obj)
    => obj is IntegrationAvailability other && Equals(other);

  public override int GetHashCode()
    => (Di ? 1 : 0) | (Logging ? 2 : 0) |
       (DiCollision ? 4 : 0) | (LoggingCollision ? 8 : 0);
}
