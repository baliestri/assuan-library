// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace AssuanLibrary.Tests.Server.Fakes;

internal sealed class FakeCommandDispatcher : ICommandDispatcher {
  public List<CommandHandler> AddedHandlers { get; } = new();

  public List<(IReadOnlyAssuanCommand command, IServerContext context)> DispatchCalls { get; } = new();

  public bool TryAddResult { get; set; } = true;

  public bool TryAdd(CommandHandler handler) {
    AddedHandlers.Add(handler);
    return TryAddResult;
  }

  public void Dispatch(IReadOnlyAssuanCommand command, IServerContext context) => DispatchCalls.Add((command, context));

  public Task DispatchAsync(IReadOnlyAssuanCommand command, IServerContext context) {
    DispatchCalls.Add((command, context));
    return Task.CompletedTask;
  }
}
