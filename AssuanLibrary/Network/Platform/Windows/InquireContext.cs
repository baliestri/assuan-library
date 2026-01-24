// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using System.Runtime.Versioning;
using AssuanLibrary.Extensions;

namespace AssuanLibrary.Network.Platform.Windows;

[SupportedOSPlatform("windows")]
internal sealed class InquireContext(string keyword, IReadOnlyCollection<string> parameters, NetworkStream networkStream) : IInquireContext {
  /// <inheritdoc />
  public string Keyword { get; } = keyword;

  /// <inheritdoc />
  public IReadOnlyCollection<string> Parameters { get; } = parameters;

  /// <inheritdoc />
  public void Write(string value) {
    var encoded = AssuanEncoder.AsBytes(value);

    networkStream.Write(Keywords.Data);
    networkStream.Write(encoded);
    networkStream.Flush();
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    var encoded = AssuanEncoder.AsReadOnlyMemory(buffer);

    networkStream.Write(Keywords.Data);
    networkStream.Write(encoded);
    networkStream.Flush();
  }

  /// <inheritdoc />
  public void End() {
    networkStream.Write(Keywords.End);
    networkStream.Flush();
  }

  /// <inheritdoc />
  public void Cancel() {
    networkStream.Write(Keywords.Cancel);
    networkStream.Flush();
  }

  /// <inheritdoc />
  public async ValueTask WriteAsync(string value, CancellationToken ct = default) {
    var encoded = AssuanEncoder.AsBytes(value);

    await networkStream.WriteAsync(Keywords.Data, ct);
    await networkStream.WriteAsync(encoded, ct);
    await networkStream.FlushAsync(ct);
  }

  /// <inheritdoc />
  public async ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default) {
    var encoded = AssuanEncoder.AsReadOnlyMemory(buffer);

    await networkStream.WriteAsync(Keywords.Data, ct);
    await networkStream.WriteAsync(encoded, ct);
    await networkStream.FlushAsync(ct);
  }

  /// <inheritdoc />
  public async ValueTask EndAsync(CancellationToken ct = default) {
    await networkStream.WriteAsync(Keywords.End, ct);
    await networkStream.FlushAsync(ct);
  }

  /// <inheritdoc />
  public async ValueTask CancelAsync(CancellationToken ct = default) {
    await networkStream.WriteAsync(Keywords.Cancel, ct);
    await networkStream.FlushAsync(ct);
  }
}
