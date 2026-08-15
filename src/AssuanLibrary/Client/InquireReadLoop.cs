// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Extensions;
using AssuanLibrary.Polyfills;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

internal static class InquireReadLoop {
  internal static byte[] Read(IAssuanConnection connection, Func<byte[], int> readByte, InquireHandler inquireHandler) {
    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();
    var buffer = new byte[1];

    while (true) {
      var bytesRead = readByte(buffer);
      if (bytesRead <= 0) {
        break; // EOF
      }

      memoryStream.WriteByte(buffer[0]);

      if (buffer[0] != Characters.LINE_FEED) {
        continue;
      }

      var responseBuffer = memoryStream.ToArray();
      var response = new AssuanResponse(responseBuffer.Take(Characters.LINE_FEED));

      finalMemoryStream.Write(responseBuffer);
      memoryStream.SetLength(0);

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new ClientInquireContext(connection, keyword, parameters);

      try {
        inquireHandler(ctx);
      }
      catch {
        ctx.Cancel();
        throw;
      }
    }

    return finalMemoryStream.ToArray();
  }

  internal static async ValueTask<ReadOnlyMemory<byte>> ReadAsync(IAssuanConnection connection,
  Func<byte[], CancellationToken, ValueTask<int>> readByteAsync, AsyncInquireHandler inquireHandler, CancellationToken ct) {
    using var finalMemoryStream = new MemoryStream();
    using var memoryStream = new MemoryStream();
    var buffer = new byte[1];

    while (true) {
      var bytesRead = await readByteAsync(buffer, ct).ConfigureAwait(false);
      if (bytesRead <= 0) {
        break; // EOF
      }

      memoryStream.WriteByte(buffer[0]);

      if (buffer[0] != Characters.LINE_FEED) {
        continue;
      }

      var responseBuffer = memoryStream.ToArray();
      var response = new AssuanResponse(responseBuffer.Take(Characters.LINE_FEED));

      finalMemoryStream.Write(responseBuffer);
      memoryStream.SetLength(0);

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new ClientInquireContext(connection, keyword, parameters);

      try {
        await inquireHandler(ctx, ct);
      }
      catch {
        await ctx.CancelAsync(ct).ConfigureAwait(false);
        throw;
      }
    }

    return finalMemoryStream.ToArray();
  }
}
