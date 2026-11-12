// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Attributes;
using AssuanLibrary.Server;

namespace AssuanLibrary.Tests;

[AssuanHandlerGroup]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global",
  Justification = "Deliberately a non-static group: the generator must support instance-capable partial classes.")]
public partial class GeneratedDemo {
  [AssuanCommandHandler("ECHO", "Echoes arguments")]
  private static ValueTask EchoAsync(CommandContext<GeneratedSessionState> context, CancellationToken token)
    => context.SendDataAsync(context.Arguments, token);

  [AssuanOptionHandler("mode")]
  [SuppressMessage("ReSharper", "UnusedParameter.Local",
    Justification = "The generator requires the full option-handler signature.")]
  private static ValueTask ModeAsync(OptionRequest request, CommandContext<GeneratedSessionState> context,
    CancellationToken token) {
    context.State.OptionCount++;
    context.State.LastValue = request.Value?.ToArray();
    return ValueTask.CompletedTask;
  }
}
