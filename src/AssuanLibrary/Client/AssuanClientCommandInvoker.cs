// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

internal sealed class AssuanClientCommandInvoker(AssuanCommandFormatter commandFormatter, AssuanResponseParser responseParser) {
  public AssuanResponseCollection Invoke(IAssuanConnection connection, AssuanCommand command) {
    var payload = commandFormatter.Format(command);
    connection.Write(payload);

    var responseBuffer = connection.Read();
    return responseParser.Parse(responseBuffer);
  }

  public AssuanResponseCollection Invoke(IAssuanConnection connection, AssuanCommand command, InquireHandler inquireHandler) {
    var payload = commandFormatter.Format(command);
    connection.Write(payload);

    var responseBuffer = connection.Read(inquireHandler);
    return responseParser.Parse(responseBuffer);
  }

  public async ValueTask<AssuanResponseCollection> InvokeAsync(IAssuanConnection connection, AssuanCommand command, CancellationToken ct = default) {
    var payload = commandFormatter.FormatAsync(command);
    await connection.WriteAsync(payload, ct).ConfigureAwait(false);

    var responseBuffer = await connection.ReadAsync(ct).ConfigureAwait(false);
    return responseParser.Parse(responseBuffer);
  }

  public async ValueTask<AssuanResponseCollection> InvokeAsync(
    IAssuanConnection connection,
    AssuanCommand command,
    AsyncInquireHandler inquireHandler,
    CancellationToken ct = default
  ) {
    var payload = commandFormatter.FormatAsync(command);
    await connection.WriteAsync(payload, ct).ConfigureAwait(false);

    var responseBuffer = await connection.ReadAsync(inquireHandler, ct).ConfigureAwait(false);
    return responseParser.Parse(responseBuffer);
  }
}

