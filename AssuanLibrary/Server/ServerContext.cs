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
public sealed class ServerContext(IAssuanConnection connection, IServerSession session, Action<IServerInquireContext> registerPipeline)
  : IServerContext {
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
  public byte[] Inquire(string keyword, IReadOnlyCollection<string> parameters) {
    var inquireContext = new ServerInquireContext(keyword, parameters);
    registerPipeline(inquireContext);

    var response = AssuanResponse.Inquire(keyword, [..parameters]);

    SendResponse(response);

    return inquireContext.Wait();
  }

  /// <inheritdoc />
  public async Task<ReadOnlyMemory<byte>> InquireAsync(string keyword, IReadOnlyCollection<string> parameters, CancellationToken ct = default) {
    var inquireContext = new ServerInquireContext(keyword, parameters);
    registerPipeline(inquireContext);

    var response = AssuanResponse.Inquire(keyword, [..parameters]);

    await SendResponseAsync(response, ct).ConfigureAwait(false);

    return await inquireContext.WaitAsync(ct).ConfigureAwait(false);
  }
}
