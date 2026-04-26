// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.ExceptionServices;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server.Dispatching;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server.Sessions;

internal sealed class AssuanSessionRunner(ICommandDispatcher commandDispatcher, AssuanServerOptions options) {
  public void Run(IAssuanConnection connection, CancellationToken cancellationToken = default) {
    ExceptionDispatchInfo? capturedException = null;

    using var sessionContext = new AssuanSessionContext(connection, cancellationToken);

    try {
      options.OnAuthenticateSessionAsync?.Invoke(sessionContext.ServerContext).GetAwaiter().GetResult();

      if (options.SendBanner) {
        sessionContext.ServerContext.SendResponse(AssuanResponse.Ok(options.Banner));
      }

      var sessionLoop = sessionContext.CreateSessionLoop(commandDispatcher);
      sessionLoop.Run();
    }
    catch (Exception ex) {
      capturedException = ExceptionDispatchInfo.Capture(ex);
    }

    capturedException?.Throw();
  }

  public async Task RunAsync(IAssuanConnection connection, CancellationToken cancellationToken = default) {
    ExceptionDispatchInfo? capturedException = null;

    await using var sessionContext = new AssuanSessionContext(connection, cancellationToken);

    try {
      if (options.OnAuthenticateSessionAsync is not null) {
        await options.OnAuthenticateSessionAsync(sessionContext.ServerContext).ConfigureAwait(false);
      }

      if (options.SendBanner) {
        await sessionContext.ServerContext.SendResponseAsync(AssuanResponse.Ok(options.Banner), cancellationToken).ConfigureAwait(false);
      }

      var sessionLoop = sessionContext.CreateSessionLoop(commandDispatcher);
      await sessionLoop.RunAsync().ConfigureAwait(false);
    }
    catch (Exception ex) {
      capturedException = ExceptionDispatchInfo.Capture(ex);
    }

    capturedException?.Throw();
  }
}
