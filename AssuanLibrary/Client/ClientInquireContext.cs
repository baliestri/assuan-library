// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>
///   Represents the context for an inquire request from the Assuan server.
/// </summary>
/// <param name="connection">The Assuan connection.</param>
/// <param name="keyword">The inquire keyword.</param>
/// <param name="parameters">The inquire parameters.</param>
public sealed class ClientInquireContext(IAssuanConnection connection, string keyword, IReadOnlyCollection<string> parameters)
  : IClientInquireContext {
  /// <inheritdoc />
  public string Keyword { get; } = keyword;

  /// <inheritdoc />
  public IReadOnlyCollection<string> Parameters { get; } = parameters;

  /// <inheritdoc />
  public void Write(string value) {
    var encoded = AssuanEncoder.AsBytes(value);
    var buffer = new byte[Commands.Data.Length + encoded.Length];
    Buffer.BlockCopy(Commands.Data, 0, buffer, 0, Commands.Data.Length);
    Buffer.BlockCopy(encoded, 0, buffer, Commands.Data.Length, encoded.Length);

    connection.Write(buffer);
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    var dataBuffer = new byte[Commands.Data.Length + buffer.Length];
    Buffer.BlockCopy(Commands.Data, 0, dataBuffer, 0, Commands.Data.Length);
    Buffer.BlockCopy(buffer, 0, dataBuffer, Commands.Data.Length, buffer.Length);

    connection.Write(dataBuffer);
  }

  /// <inheritdoc />
  public void End()
    => connection.Write(Commands.End);

  /// <inheritdoc />
  public void Cancel()
    => connection.Write(Commands.Cancel);

  /// <inheritdoc />
  public async ValueTask WriteAsync(string value, CancellationToken ct = default) {
    var encoded = AssuanEncoder.AsBytes(value);
    var buffer = new byte[Commands.Data.Length + encoded.Length];
    Buffer.BlockCopy(Commands.Data, 0, buffer, 0, Commands.Data.Length);
    Buffer.BlockCopy(encoded, 0, buffer, Commands.Data.Length, encoded.Length);

    await connection.WriteAsync(buffer, ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default) {
    var dataBuffer = new byte[Commands.Data.Length + buffer.Length];
    Buffer.BlockCopy(Commands.Data, 0, dataBuffer, 0, Commands.Data.Length);
    Buffer.BlockCopy(buffer, 0, dataBuffer, Commands.Data.Length, buffer.Length);

    await connection.WriteAsync(dataBuffer, ct).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async ValueTask EndAsync(CancellationToken ct = default)
    => await connection.WriteAsync(Commands.End, ct).ConfigureAwait(false);

  /// <inheritdoc />
  public async ValueTask CancelAsync(CancellationToken ct = default)
    => await connection.WriteAsync(Commands.Cancel, ct).ConfigureAwait(false);
}
