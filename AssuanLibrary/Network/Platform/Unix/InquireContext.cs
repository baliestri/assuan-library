// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using System.Runtime.Versioning;

namespace AssuanLibrary.Network.Platform.Unix;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class InquireContext(string keyword, IReadOnlyCollection<string> parameters, Socket socket) : IInquireContext {
  /// <inheritdoc />
  public string Keyword { get; } = keyword;

  /// <inheritdoc />
  public IReadOnlyCollection<string> Parameters { get; } = parameters;

  /// <inheritdoc />
  public void Write(string value) {
    var encoded = AssuanEncoder.AsBytes(value);
    var buffer = new byte[Keywords.Data.Length + encoded.Length];
    Buffer.BlockCopy(Keywords.Data, 0, buffer, 0, Keywords.Data.Length);
    Buffer.BlockCopy(encoded, 0, buffer, Keywords.Data.Length, encoded.Length);

    socket.Send(buffer, SocketFlags.None);
  }

  /// <inheritdoc />
  public void Write(byte[] buffer) {
    var dataBuffer = new byte[Keywords.Data.Length + buffer.Length];
    Buffer.BlockCopy(Keywords.Data, 0, dataBuffer, 0, Keywords.Data.Length);
    Buffer.BlockCopy(buffer, 0, dataBuffer, Keywords.Data.Length, buffer.Length);

    socket.Send(dataBuffer, SocketFlags.None);
  }

  /// <inheritdoc />
  public void End()
    => socket.Send(Keywords.End, SocketFlags.None);

  /// <inheritdoc />
  public void Cancel()
    => socket.Send(Keywords.Cancel, SocketFlags.None);

  /// <inheritdoc />
  public async ValueTask WriteAsync(string value, CancellationToken ct = default) {
    var encoded = AssuanEncoder.AsBytes(value);
    var buffer = new byte[Keywords.Data.Length + encoded.Length];
    Buffer.BlockCopy(Keywords.Data, 0, buffer, 0, Keywords.Data.Length);
    Buffer.BlockCopy(encoded, 0, buffer, Keywords.Data.Length, encoded.Length);
    var segment = new ArraySegment<byte>(buffer);

    await socket.SendAsync(segment, SocketFlags.None);
  }

  /// <inheritdoc />
  public async ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default) {
    var dataBuffer = new byte[Keywords.Data.Length + buffer.Length];
    Buffer.BlockCopy(Keywords.Data, 0, dataBuffer, 0, Keywords.Data.Length);
    Buffer.BlockCopy(buffer, 0, dataBuffer, Keywords.Data.Length, buffer.Length);
    var segment = new ArraySegment<byte>(dataBuffer);

    await socket.SendAsync(segment, SocketFlags.None);
  }

  /// <inheritdoc />
  public async ValueTask EndAsync(CancellationToken ct = default) {
    var segment = new ArraySegment<byte>(Keywords.End);
    await socket.SendAsync(segment, SocketFlags.None);
  }

  /// <inheritdoc />
  public async ValueTask CancelAsync(CancellationToken ct = default) {
    var segment = new ArraySegment<byte>(Keywords.Cancel);
    await socket.SendAsync(segment, SocketFlags.None);
  }
}
