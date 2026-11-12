// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;

if (args is not ["--homedir", var _, "--list-dirs", "agent-socket"]) {
  return 2;
}

var home = args[1];

File.WriteAllLines(Path.Combine(home, "arguments.txt"), args);

var mode = await File.ReadAllTextAsync(Path.Combine(home, "mode.txt")).ConfigureAwait(false);
var output = await File.ReadAllBytesAsync(Path.Combine(home, "output.bin")).ConfigureAwait(false);
var stdout = Console.OpenStandardOutput();
var stderr = Console.OpenStandardError();

switch (mode) {
  case "failure":
    stdout.Write(output);
    return 23;
  case "stderr":
    stderr.Write(new byte[1024 * 1024 + 1]);
    return 0;
  case "aggregate":
    stdout.Write(new byte[600_000]);
    stderr.Write(new byte[600_000]);
    return 0;
  case "timeout":
    await File.WriteAllTextAsync(Path.Combine(home, "pid.txt"),
      Environment.ProcessId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
    await Task.Delay(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
    await File.WriteAllTextAsync(Path.Combine(home, "completed.txt"), "unexpected").ConfigureAwait(false);
    return 0;
  default:
    stdout.Write(output);
    return 0;
}
