// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Server.Abstractions;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Server;

/// <summary>
///   Represents the context of an Assuan server session.
/// </summary>
/// <param name="connection">The Assuan connection.</param>
/// <param name="session">The Assuan session.</param>
public sealed class ServerContext(IAssuanConnection connection, IServerSession session) : IServerContext {
  /// <inheritdoc />
  public IServerSession Session { get; } = session;

  /// <inheritdoc />
  public void SendResponse(AssuanResponseCollection responseCollection)
    => connection.Write(responseCollection.GetOriginalBuffer());

  /// <inheritdoc />
  public void SendResponse(AssuanResponse response)
    => connection.Write(response.GetOriginalBuffer());

  /// <inheritdoc />
  public async Task SendResponseAsync(AssuanResponseCollection responseCollection, CancellationToken ct = default)
    => await connection.WriteAsync(responseCollection.GetOriginalBuffer(), ct).ConfigureAwait(false);

  /// <inheritdoc />
  public async Task SendResponseAsync(AssuanResponse response, CancellationToken ct = default)
    => await connection.WriteAsync(response.GetOriginalBuffer(), ct).ConfigureAwait(false);

  /// <inheritdoc />
  public byte[] SendResponse(string keyword, IReadOnlyCollection<string> parameters, InquireHandler inquireHandler) {
    var inquireContext = new ServerInquireContext(keyword, parameters);
    Session.Items["__inquire_context__"] = inquireContext;

    var response = AssuanResponse.Inquire(keyword, [..parameters]);

    SendResponse(response);
    inquireHandler(inquireContext);

    return inquireContext.Wait();
  }

  /// <inheritdoc />
  public async Task<ReadOnlyMemory<byte>> SendResponseAsync(string keyword, IReadOnlyCollection<string> parameters,
  AsyncInquireHandler inquireHandler, CancellationToken ct = default) {
    var inquireContext = new ServerInquireContext(keyword, parameters);
    Session.Items["__inquire_context__"] = inquireContext;

    var response = AssuanResponse.Inquire(keyword, [..parameters]);

    await SendResponseAsync(response, ct).ConfigureAwait(false);
    await inquireHandler(inquireContext, ct);

    return await inquireContext.WaitAsync(ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public void Close()
    => connection.Dispose();

  /// <inheritdoc />
  public async Task CloseAsync(CancellationToken ct = default)
    => await connection.DisposeAsync().ConfigureAwait(false);
}
