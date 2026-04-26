// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Transport.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssuanLibrary.Sample;

public sealed class AssuanServerBackgroundService(IServiceProvider serviceProvider) : BackgroundService {
  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    var server = serviceProvider.GetRequiredService<IAssuanServer>();
    var endpoint = serviceProvider.GetRequiredService<IAssuanEndpoint>();
    server.RegisterCommandHandler<CustomServer.GetInfoCommandHandler>();

    await server.RunAsync(endpoint, stoppingToken);
  }
}
