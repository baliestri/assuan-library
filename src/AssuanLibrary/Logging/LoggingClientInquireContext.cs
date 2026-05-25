// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Logging;

internal sealed class LoggingClientInquireContext(
  IClientInquireContext inner,
  LoggingAssuanConnection connectionLogger,
  string direction
) : IClientInquireContext {
  public string Keyword => inner.Keyword;

  public IReadOnlyCollection<string> Parameters => inner.Parameters;

  public void Write(string value) {
    inner.Write(value);
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, CreateDataBuffer(AssuanEncoder.AsBytes(value, false)), direction);
  }

  public void Write(byte[] buffer) {
    inner.Write(buffer);
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, CreateDataBuffer(buffer), direction);
  }

  public void End() {
    inner.End();
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, Commands.End, direction);
  }

  public void Cancel() {
    inner.Cancel();
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, Commands.Cancel, direction);
  }

  public async ValueTask WriteAsync(string value, CancellationToken ct = default) {
    await inner.WriteAsync(value, ct).ConfigureAwait(false);
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, CreateDataBuffer(AssuanEncoder.AsBytes(value, false)), direction);
  }

  public async ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default) {
    await inner.WriteAsync(buffer, ct).ConfigureAwait(false);
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, CreateDataBuffer(buffer), direction);
  }

  public async ValueTask EndAsync(CancellationToken ct = default) {
    await inner.EndAsync(ct).ConfigureAwait(false);
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, Commands.End, direction);
  }

  public async ValueTask CancelAsync(CancellationToken ct = default) {
    await inner.CancelAsync(ct).ConfigureAwait(false);
    connectionLogger.LogTraffic(AssuanLogEvents.MessageSent, Commands.Cancel, direction);
  }

  private static byte[] CreateDataBuffer(byte[] buffer) {
    var dataBuffer = new byte[Commands.Data.Length + buffer.Length];
    Buffer.BlockCopy(Commands.Data, 0, dataBuffer, 0, Commands.Data.Length);
    Buffer.BlockCopy(buffer, 0, dataBuffer, Commands.Data.Length, buffer.Length);

    return dataBuffer;
  }
}
