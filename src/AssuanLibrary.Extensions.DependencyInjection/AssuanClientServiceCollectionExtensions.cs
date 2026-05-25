// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssuanLibrary.Extensions.DependencyInjection;

/// <summary>
///   Extensions for registering Assuan client services in an <see cref="IServiceCollection" />.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AssuanClientServiceCollectionExtensions {
  /// <summary>
  ///   Registers the Assuan client services in the provided <see cref="IServiceCollection" /> with default endpoint resolver, connection factory, and
  ///   options.
  /// </summary>
  /// <param name="configureOptions">An action to configure the <see cref="AssuanClientOptions" />.</param>
  /// <param name="serviceCollection">The service collection to register the services in.</param>
  /// <returns>The service collection itself.</returns>
  public static IServiceCollection AddAssuanClient(this IServiceCollection serviceCollection, Action<AssuanClientOptions>? configureOptions) {
    serviceCollection.TryAddSingleton(serviceProvider => {
      var options = AssuanClientOptions.CreateDefault();
      configureOptions?.Invoke(options);
      options.Logging.UseMicrosoftLoggerWhenAvailable(serviceProvider, "AssuanLibrary.Client");
      return options;
    });

    serviceCollection.TryAddTransient<IAssuanClient, AssuanClient>();

    return serviceCollection;
  }
}
