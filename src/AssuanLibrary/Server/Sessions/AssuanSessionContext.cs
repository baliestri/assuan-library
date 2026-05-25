// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Server.Dispatching;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server.Sessions;

internal sealed class AssuanSessionContext : IDisposable, IAsyncDisposable {
  private AssuanSessionLoop? _sessionLoop;

  public AssuanSessionContext(IAssuanConnection connection, CancellationToken cancellationToken = default) {
    Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    Session = new ServerSession(cancellationToken);
    ServerContext = new ServerContext(Connection, Session, inquire => _sessionLoop?.SetActiveInquire(inquire));
  }

  public IAssuanConnection Connection { get; }

  public IServerSession Session { get; }

  public IServerContext ServerContext { get; }

  public async ValueTask DisposeAsync() {
    Session.Dispose();
    await Connection.DisposeAsync().ConfigureAwait(false);
  }

  public void Dispose() {
    Session.Dispose();
    Connection.Dispose();
  }

  public AssuanSessionLoop CreateSessionLoop(ICommandDispatcher commandDispatcher)
    => _sessionLoop = new AssuanSessionLoop(Connection, Session, ServerContext, commandDispatcher);
}
