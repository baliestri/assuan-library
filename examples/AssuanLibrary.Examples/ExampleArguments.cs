// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;

namespace AssuanLibrary.Examples;

internal static class ExampleArguments {
  internal static bool TryPort(string[] args, TextWriter error, out int port) {
    port = 0;
    if (args is ["--port", var _] &&
        ushort.TryParse(args[1], NumberStyles.None,
          CultureInfo.InvariantCulture, out var value) &&
        value > 0) {
      port = value;
      return true;
    }

    error.WriteLine("Specify --port <1..65535> for a loopback example.");
    return false;
  }
}
