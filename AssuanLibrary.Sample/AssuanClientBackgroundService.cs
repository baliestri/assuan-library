// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssuanLibrary.Sample;

public sealed class AssuanClientBackgroundService(IServiceProvider serviceProvider) : BackgroundService {
  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    var client = serviceProvider.GetRequiredService<IAssuanClient>();
    var endpoint = serviceProvider.GetRequiredService<IAssuanEndpoint>();
    var logger = serviceProvider.GetRequiredService<ILogger<AssuanClientBackgroundService>>();

    await client.ConnectAsync(endpoint, new Dictionary<string, object>(), stoppingToken);

    // Just sending a command as an example
    var command = new AssuanCommand("GETINFO") { "version" };
    var responseCollection = await client.InvokeAsync(command, stoppingToken);
    foreach (var response in responseCollection) {
      logger.LogInformation("Response: {Response:GT}", response);
    }
  }
}
