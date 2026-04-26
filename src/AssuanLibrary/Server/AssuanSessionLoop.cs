// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

internal sealed class AssuanSessionLoop(
  IAssuanConnection connection,
  IServerSession session,
  IServerContext context,
  ICommandDispatcher commandDispatcher
) {
  private IServerInquireContext? _activeInquireContext;

  public void SetActiveInquire(IServerInquireContext inquire) {
    if (_activeInquireContext is not null) {
      throw new InvalidOperationException("Only one INQUIRE can be active per session.");
    }

    _activeInquireContext = inquire;
  }

  public void Run() {
    while (!session.CancellationToken.IsCancellationRequested) {
      var buffer = connection.ReadAvailable();

      if (buffer.Length == 0) {
        continue;
      }

      session.RefreshLastActivity();

      if (_activeInquireContext is not null) {
        var inquireResponseCollection = new AssuanResponseCollection(buffer);

        foreach (var inquireResponse in inquireResponseCollection) {
          switch (inquireResponse.Type) {
            case AssuanResponseType.Data:
              _activeInquireContext?.Receive(inquireResponse.Buffer);
              continue;
            case AssuanResponseType.End:
              _activeInquireContext?.Complete();
              _activeInquireContext = null;
              continue;
            case AssuanResponseType.Cancel:
              _activeInquireContext?.Cancel();
              _activeInquireContext = null;
              context.SendResponse(AssuanResponse.Error(99, "Operation canceled"));
              continue;
            default:
              continue;
          }
        }

        continue;
      }

      var command = new AssuanCommand(buffer);
      commandDispatcher.Dispatch(command, context);
    }
  }

  public async Task RunAsync() {
    while (!session.CancellationToken.IsCancellationRequested) {
      var buffer = await connection.ReadAvailableAsync(session.CancellationToken).ConfigureAwait(false);

      if (buffer.IsEmpty) {
        continue;
      }

      session.RefreshLastActivity();

      if (_activeInquireContext is not null) {
        var inquireResponseCollection = new AssuanResponseCollection(buffer);

        foreach (var inquireResponse in inquireResponseCollection) {
          switch (inquireResponse.Type) {
            case AssuanResponseType.Data:
              _activeInquireContext?.Receive(inquireResponse.Buffer);
              continue;
            case AssuanResponseType.End:
              _activeInquireContext?.Complete();
              _activeInquireContext = null;
              continue;
            case AssuanResponseType.Cancel:
              _activeInquireContext?.Cancel();
              _activeInquireContext = null;
              await context.SendResponseAsync(AssuanResponse.Error(99, "Operation canceled"), session.CancellationToken);
              continue;
            default:
              continue;
          }
        }

        continue;
      }

      var command = new AssuanCommand(buffer.ToArray());
      _ = commandDispatcher.DispatchAsync(command, context).ConfigureAwait(false);
    }
  }
}
