// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Client;
using AssuanLibrary.Protocol;
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

while (!cts.IsCancellationRequested) {
  var line = Console.ReadLine();
  var command = new AssuanCommand(Encoding.UTF8.GetBytes(line + '\n'));

  var responseCollection = await client.InvokeAsync(command);
  foreach (var response in responseCollection) {
    Console.WriteLine($"Response: {response}");
  }
}

cts.Cancel();
await serverTask;
// --------------------------------------------
