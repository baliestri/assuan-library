// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Attributes;
using AssuanLibrary.Server;

namespace AssuanLibrary.Examples;

[AssuanHandlerGroup]
internal static partial class TcpExampleHandlers {
  [AssuanCommandHandler("ECHO", "Echo wire arguments")]
  private static ValueTask EchoAsync(CommandContext<TcpExampleState> context, CancellationToken token)
    => context.SendDataAsync(context.Arguments, token);
}
