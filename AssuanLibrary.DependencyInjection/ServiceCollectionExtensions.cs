// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Platform.Common.Transport;
using AssuanLibrary.Platform.Unix.Transport;
using AssuanLibrary.Platform.Windows.Transport;
using AssuanLibrary.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssuanLibrary.DependencyInjection;

/// <summary>
///   Extensions for registering services in an <see cref="IServiceCollection" />.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions {
  /// <summary>
  ///   Registers TCP client factories in the provided <see cref="IServiceCollection" />.
  /// </summary>
  /// <param name="serviceCollection">The service collection to register the services in.</param>
  /// <returns>The service collection itself.</returns>
  public static IServiceCollection AddTcpClientFactories(this IServiceCollection serviceCollection) {
    serviceCollection.TryAddSingleton<IAssuanListenerFactory, TcpClientListenerFactory>();
    serviceCollection.TryAddSingleton<IAssuanConnectionFactory, TcpClientConnectionFactory>();

    return serviceCollection;
  }

  /// <summary>
  ///   Registers Unix Domain Socket factories in the provided <see cref="IServiceCollection" />.
  /// </summary>
  /// <param name="serviceCollection">The service collection to register the services in.</param>
  /// <returns>The service collection itself.</returns>
  public static IServiceCollection AddUnixDomainSocketFactories(this IServiceCollection serviceCollection) {
    serviceCollection.TryAddSingleton<IAssuanListenerFactory, UnixDomainSocketListenerFactory>();
    serviceCollection.TryAddSingleton<IAssuanConnectionFactory, UnixDomainSocketConnectionFactory>();

    return serviceCollection;
  }

  /// <summary>
  ///   Registers Named Pipe factories in the provided <see cref="IServiceCollection" />.
  /// </summary>
  /// <param name="serviceCollection">The service collection to register the services in.</param>
  /// <returns>The service collection itself.</returns>
  public static IServiceCollection AddNamedPipeFactories(this IServiceCollection serviceCollection) {
    serviceCollection.TryAddSingleton<IAssuanListenerFactory, NamedPipeListenerFactory>();
    serviceCollection.TryAddSingleton<IAssuanConnectionFactory, NamedPipeConnectionFactory>();

    return serviceCollection;
  }
}
