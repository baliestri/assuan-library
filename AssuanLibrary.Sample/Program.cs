// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Sample;
using AssuanLibrary.Server;

using var cts = new CancellationTokenSource();

// --------- CUSTOM SERVER & CLIENT SAMPLE ---------
// var serverTask = CustomServer.RunAsync(cts.Token);
// await Task.Delay(300);
// await CustomServer.RunClientAsync();
//
// cts.Cancel();
// await serverTask;
// -------------------------------------------------

// --------- AGENT CLIENT SAMPLE ---------
// const string KEYGRIP = "A1B2C3D4F5G6..."; // ← replace with a real keygrip from your setup
// await AgentClient.RunClientAsync(KEYGRIP);
// --------------------------------------

// --------- CONSOLE TRANSPORT SAMPLE ---------
var endpoint = ConsoleEndpoint.Instance;
var server = new AssuanServer(new ConsoleListenerFactory());
server.RegisterCommandHandler<CustomServer.GetInfoCommandHandler>();
server.RegisterCommandHandler<CustomServer.ByeCommandHandler>();
var serverTask = server.RunAsync(endpoint, cts.Token);

var client = new AssuanClient(new ConsoleConnectionFactory());
await client.ConnectAsync(endpoint, new Dictionary<string, object>());

cts.Cancel();
await serverTask;
// --------------------------------------------

// --------- DEPENDENCY INJECTION SAMPLE ---------
// var builder = Host.CreateApplicationBuilder(args);
// var endpoint = new TcpClientEndpoint(IPAddress.Loopback, 12345);
// builder.Services.AddAssuanServer(options => {
//   options.Banner = "Hosting Assuan Server via Dependency Injection";
// });
// builder.Services.AddAssuanClient(options => { });
// builder.Services.AddSingleton<IAssuanEndpoint>(endpoint);
// builder.Services.AddHostedService<AssuanServerBackgroundService>();
// builder.Services.AddHostedService<AssuanClientBackgroundService>(); // Should not be used like this
//
// var host = builder.Build();
//
// await host.RunAsync(cts.Token);
// -----------------------------------------------
