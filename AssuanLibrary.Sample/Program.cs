// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Sample;

using var cts = new CancellationTokenSource();

// --------- CUSTOM SERVER & CLIENT SAMPLE ---------
var serverTask = CustomServer.RunAsync(cts.Token);
await Task.Delay(300);
await CustomServer.RunClientAsync();

cts.Cancel();
await serverTask;
// -------------------------------------------------

// --------- AGENT CLIENT SAMPLE ---------
// const string KEYGRIP = "A1B2C3D4F5G6..."; // ← replace with a real keygrip from your setup
// await AgentClient.RunClientAsync(KEYGRIP);
// --------------------------------------
