// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Examples;

using var stop = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, eventArgs) => {
  eventArgs.Cancel = true;
  stop.Cancel();
};

Console.CancelKeyPress += cancel;

try {
  return await ExampleCatalog.RunAsync(args, Console.In, Console.Out, Console.Error, stop.Token);
}
finally {
  Console.CancelKeyPress -= cancel;
}
