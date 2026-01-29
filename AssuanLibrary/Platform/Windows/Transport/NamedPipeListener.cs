// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Pipes;
using System.Runtime.Versioning;
using AssuanLibrary.Platform.Windows.Transport.Endpoints;
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Platform.Windows.Transport;

[SupportedOSPlatform("windows")]
internal sealed class NamedPipeListener(NamedPipeEndpoint endpoint, AssuanListenerOptions options) : IAssuanListener {
  /// <inheritdoc />
  public IAssuanEndpoint Endpoint { get; } = endpoint;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    var currentServerStream = new NamedPipeServerStream(endpoint.Name, PipeDirection.InOut, -1, PipeTransmissionMode.Message,
      PipeOptions.None);
    currentServerStream.WaitForConnection();

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    var connection = new NamedPipeConnection(currentServerStream, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);

    return connection;
  }

  /// <inheritdoc />
  public async ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = default) {
    var currentServerStream = new NamedPipeServerStream(endpoint.Name, PipeDirection.InOut, -1, PipeTransmissionMode.Message,
      PipeOptions.Asynchronous);
    await currentServerStream.WaitForConnectionAsync(ct).ConfigureAwait(false);

    var stabilizationOptions = StabilizationOptions.Default;
    options.ConfigureStabilization?.Invoke(stabilizationOptions);

    var connection = new NamedPipeConnection(currentServerStream, endpoint, AssuanConnectionOptions.Default, stabilizationOptions);

    return connection;
  }
}
