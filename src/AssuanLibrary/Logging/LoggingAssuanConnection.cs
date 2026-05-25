// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Extensions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Logging;

internal sealed class LoggingAssuanConnection(
  IAssuanConnection inner,
  AssuanLoggingOptions loggingOptions,
  AssuanConnectionLoggingRole role
) : IAssuanConnection {
  public static IAssuanConnection Wrap(IAssuanConnection connection, AssuanLoggingOptions? loggingOptions, AssuanConnectionLoggingRole role) {
    var effectiveOptions = loggingOptions ?? AssuanLoggingOptions.CreateDefault();

    return effectiveOptions.Logger.IsEnabled(AssuanLogLevel.Debug)
      ? new LoggingAssuanConnection(connection, effectiveOptions, role)
      : connection;
  }

  public bool IsConnected => inner.IsConnected;

  public void Open() {
    inner.Open();
    LogConnection(AssuanLogEvents.ConnectionOpened, "Assuan connection opened.");
  }

  public void Write(byte[] buffer) {
    inner.Write(buffer);
    LogTraffic(AssuanLogEvents.MessageSent, buffer, GetWriteDirection());
  }

  public byte[] Read() {
    var buffer = inner.Read();
    LogTraffic(AssuanLogEvents.MessageReceived, buffer, GetReadDirection());
    return buffer;
  }

  public byte[] Read(InquireHandler inquireHandler) {
    var buffer = inner.Read(context => inquireHandler(new LoggingClientInquireContext(context, this, GetWriteDirection())));
    LogTraffic(AssuanLogEvents.MessageReceived, buffer, GetReadDirection());
    return buffer;
  }

  public byte[] ReadAvailable() {
    var buffer = inner.ReadAvailable();
    LogTraffic(AssuanLogEvents.MessageReceived, buffer, GetReadDirection());
    return buffer;
  }

  public void DiscardPendingInput()
    => inner.DiscardPendingInput();

  public void Close() {
    inner.Close();
    LogConnection(AssuanLogEvents.ConnectionClosed, "Assuan connection closed.");
  }

  public async Task OpenAsync(CancellationToken ct = default) {
    await inner.OpenAsync(ct).ConfigureAwait(false);
    LogConnection(AssuanLogEvents.ConnectionOpened, "Assuan connection opened.");
  }

  public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
    await inner.WriteAsync(buffer, ct).ConfigureAwait(false);
    LogTraffic(AssuanLogEvents.MessageSent, buffer.ToArray(), GetWriteDirection());
  }

  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = default) {
    var buffer = await inner.ReadAsync(ct).ConfigureAwait(false);
    LogTraffic(AssuanLogEvents.MessageReceived, buffer.ToArray(), GetReadDirection());
    return buffer;
  }

  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler, CancellationToken ct = default) {
    var buffer = await inner.ReadAsync(
      async (context, token)
        => await inquireHandler(new LoggingClientInquireContext(context, this, GetWriteDirection()), token).ConfigureAwait(false),
      ct
    ).ConfigureAwait(false);

    LogTraffic(AssuanLogEvents.MessageReceived, buffer.ToArray(), GetReadDirection());
    return buffer;
  }

  public async ValueTask<ReadOnlyMemory<byte>> ReadAvailableAsync(CancellationToken ct = default) {
    var buffer = await inner.ReadAvailableAsync(ct).ConfigureAwait(false);
    LogTraffic(AssuanLogEvents.MessageReceived, buffer.ToArray(), GetReadDirection());
    return buffer;
  }

  public ValueTask DiscardPendingInputAsync(CancellationToken ct = default)
    => inner.DiscardPendingInputAsync(ct);

  public async Task CloseAsync(CancellationToken ct = default) {
    await inner.CloseAsync(ct).ConfigureAwait(false);
    LogConnection(AssuanLogEvents.ConnectionClosed, "Assuan connection closed.");
  }

  public void Dispose()
    => inner.Dispose();

  public ValueTask DisposeAsync()
    => inner.DisposeAsync();

  internal void LogTraffic(AssuanEventId eventId, byte[] buffer, string direction) {
    if (buffer.Length == 0 ||
        !loggingOptions.Logger.IsEnabled(AssuanLogLevel.Debug)) {
      return;
    }

    foreach (var frame in buffer.Split(Characters.LINE_FEED)) {
      if (frame.Length == 0) {
        continue;
      }

      var message = FormatFrame(frame, direction);
      loggingOptions.Logger.Log(AssuanLogLevel.Debug, eventId, message, null, static (state, _) => state);
    }
  }

  private void LogConnection(AssuanEventId eventId, string message) {
    if (!loggingOptions.Logger.IsEnabled(AssuanLogLevel.Debug)) {
      return;
    }

    loggingOptions.Logger.Log(AssuanLogLevel.Debug, eventId, message, null, static (state, _) => state);
  }

  private string FormatFrame(byte[] frame, string direction) {
    var response = new AssuanResponse(frame);
    var description = response.Type is not AssuanResponseType.Unknown
      ? $"response {response.Type.ToStringRepresentation()}"
      : FormatCommandDescription(frame);

    return loggingOptions.PayloadMode switch {
      AssuanPayloadLoggingMode.None => $"{direction} {description} ({frame.Length} bytes)",
      AssuanPayloadLoggingMode.Raw => $"{direction} {description} ({frame.Length} bytes) payload=\"{FormatRawPayload(frame)}\"",
      var _ => $"{direction} {description} ({frame.Length} bytes) payload=<redacted>"
    };
  }

  private string FormatCommandDescription(byte[] frame) {
    try {
      var command = new AssuanCommand(frame);
      return $"command {command.Name}";
    }
    catch {
      return "message Unknown";
    }
  }

  private string FormatRawPayload(byte[] frame) {
    var payload = AssuanDecoder.ToString(frame).TrimEnd('\r', '\n');
    var maxLength = Math.Max(0, loggingOptions.MaxPayloadLength);

    if (payload.Length <= maxLength) {
      return payload;
    }

    return payload[..maxLength] + "...";
  }

  private string GetReadDirection()
    => role is AssuanConnectionLoggingRole.Client ? "Server -> Client" : "Client -> Server";

  private string GetWriteDirection()
    => role is AssuanConnectionLoggingRole.Client ? "Client -> Server" : "Server -> Client";
}
