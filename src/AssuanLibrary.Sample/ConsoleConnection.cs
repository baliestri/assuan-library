// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Extensions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Sample;

public sealed class ConsoleConnection : IAssuanConnection {
  /// <inheritdoc />
  public bool IsConnected => true;

  /// <inheritdoc />
  public void Open() { }

  /// <inheritdoc />
  public void Write(byte[] buffer)
    => Console.Write(Encoding.UTF8.GetString(buffer));

  /// <inheritdoc />
  public byte[] Read() {
    var input = Console.ReadLine();
    return input is null
      ? []
      : Encoding.UTF8.GetBytes(input + "\n");
  }

  /// <inheritdoc />
  public byte[] Read(InquireHandler inquireHandler) {
    using var buffer = new MemoryStream();

    while (true) {
      var line = Console.ReadLine();
      if (string.IsNullOrWhiteSpace(line)) {
        break;
      }

      var bytes = Encoding.UTF8.GetBytes(line + "\n");
      buffer.Write(bytes);

      var response = new AssuanResponse(bytes.Take((byte)0x0A));

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new ClientInquireContext(this, keyword, parameters);

      try {
        inquireHandler(ctx);
      }
      catch {
        ctx.Cancel();
        throw;
      }
    }

    return buffer.ToArray();
  }

  /// <inheritdoc />
  public byte[] ReadAvailable() {
    var input = Console.ReadLine();
    return input is null
      ? []
      : Encoding.UTF8.GetBytes(input + "\n");
  }

  /// <inheritdoc />
  public void DiscardPendingInput()
    => throw new NotImplementedException();

  /// <inheritdoc />
  public void Close() { }

  /// <inheritdoc />
  public Task OpenAsync(CancellationToken ct = default)
    => Task.CompletedTask;

  /// <inheritdoc />
  public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
    Console.Write(Encoding.UTF8.GetString(buffer.Span));
    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    var input = Console.ReadLine();
    return input is null
      ? new ValueTask<ReadOnlyMemory<byte>>(new ReadOnlyMemory<byte>([]))
      : new ValueTask<ReadOnlyMemory<byte>>(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(input + "\n")));
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler, CancellationToken ct = default) {
    using var buffer = new MemoryStream();

    while (!ct.IsCancellationRequested) {
      var line = Console.ReadLine();
      if (string.IsNullOrWhiteSpace(line)) {
        break;
      }

      var bytes = Encoding.UTF8.GetBytes(line + "\n");
      buffer.Write(bytes);

      var response = new AssuanResponse(bytes.Take((byte)0x0A));

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new ClientInquireContext(this, keyword, parameters);

      try {
        await inquireHandler(ctx, ct);
      }
      catch {
        await ctx.CancelAsync(ct).ConfigureAwait(false);
        throw;
      }
    }

    return buffer.ToArray();
  }

  /// <inheritdoc />
  public ValueTask<ReadOnlyMemory<byte>> ReadAvailableAsync(CancellationToken ct = default) {
    var input = Console.ReadLine();
    return input is null
      ? new ValueTask<ReadOnlyMemory<byte>>(new ReadOnlyMemory<byte>([]))
      : new ValueTask<ReadOnlyMemory<byte>>(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(input + "\n")));
  }

  /// <inheritdoc />
  public ValueTask DiscardPendingInputAsync(CancellationToken ct = default)
    => throw new NotImplementedException();

  /// <inheritdoc />
  public Task CloseAsync(CancellationToken ct = default)
    => Task.CompletedTask;

  /// <inheritdoc />
  public void Dispose() {
    // TODO release managed resources here
  }

  /// <inheritdoc />
  public ValueTask DisposeAsync() {
    // TODO release managed resources here
    return ValueTask.CompletedTask;
  }
}
