// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Sample;

public sealed class ConsoleListener : IAssuanListener {
  private bool _accepted;

  /// <inheritdoc />
  public IAssuanEndpoint Endpoint { get; } = ConsoleEndpoint.Instance;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    if (_accepted) {
      throw new NotSupportedException("Only one connection is supported.");
    }

    _accepted = true;
    return new ConsoleConnection();
  }

  /// <inheritdoc />
  public ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
    if (_accepted) {
      throw new NotSupportedException("Only one connection is supported.");
    }

    _accepted = true;
    return new ValueTask<IAssuanConnection>(new ConsoleConnection());
  }
}
