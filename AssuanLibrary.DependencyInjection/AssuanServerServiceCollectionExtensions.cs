// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Server;
using AssuanLibrary.Server.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssuanLibrary.DependencyInjection;

/// <summary>
///   Extensions for registering Assuan server services in an <see cref="IServiceCollection" />.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AssuanServerServiceCollectionExtensions {
  /// <summary>
  ///   Registers the Assuan server services in the provided <see cref="IServiceCollection" /> with the specified listener factory, command dispatcher, and
  ///   configuration options.
  /// </summary>
  /// <param name="configureOptions">An action to configure the <see cref="AssuanServerOptions" />.</param>
  /// <param name="serviceCollection">The service collection to register the services in.</param>
  /// <returns>The service collection itself.</returns>
  public static IServiceCollection AddAssuanServer(this IServiceCollection serviceCollection, Action<AssuanServerOptions>? configureOptions) {
    var options = AssuanServerOptions.Default;
    configureOptions?.Invoke(options);

    serviceCollection.TryAddSingleton(options);
    serviceCollection.TryAddSingleton<IAssuanServer, AssuanServer>();

    return serviceCollection;
  }
}
