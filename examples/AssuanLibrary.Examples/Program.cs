// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Examples;

using var stop = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, eventArgs) =>
{
  eventArgs.Cancel = true;
  // The handler is unsubscribed in the finally below, before the source is disposed.
  // ReSharper disable once AccessToDisposedClosure
  stop.Cancel();
};

Console.CancelKeyPress += cancel;

try {
  return await ExampleCatalog.RunAsync(args, Console.In, Console.Out, Console.Error, stop.Token).ConfigureAwait(false);
}
finally {
  Console.CancelKeyPress -= cancel;
}
