// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Windows;

/// <summary>Connects to a local Windows byte pipe without a blocking wait worker.</summary>
public static class PipeConnector {
  /// <summary>Retries a busy pipe asynchronously under one total deadline, ten seconds by default.</summary>
  public static async Task<Stream> ConnectAsync(LocalEndpoint endpoint, TimeSpan? timeout = null,
  CancellationToken cancellationToken = default) {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    ArgumentNullException.ThrowIfNull(endpoint);

    if (endpoint.Kind != LocalEndpointKind.NamedPipe) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    var deadline = new TransportDeadline(timeout ?? TimeSpan.FromSeconds(10));
    if (cancellationToken.IsCancellationRequested) {
      throw new TransportException(TransportErrorKind.Cancelled);
    }

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    linked.CancelAfter(deadline.Remaining);
    while (true) {
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new TransportException(TransportErrorKind.Timeout);
      }

      if (cancellationToken.IsCancellationRequested) {
        throw new TransportException(TransportErrorKind.Cancelled);
      }

      var pipe = PipeNative.TryConnect(endpoint.Path);
      if (pipe is not null) {
        if (deadline.Remaining != TimeSpan.Zero &&
            !cancellationToken.IsCancellationRequested) {
          return new PipeByteStream(pipe);
        }

        await pipe.DisposeAsync();
        throw new TransportException(cancellationToken.IsCancellationRequested
          ? TransportErrorKind.Cancelled
          : TransportErrorKind.Timeout);
      }

      try {
        await Task.Delay(TimeSpan.FromMilliseconds(10), linked.Token).ConfigureAwait(false);
      }
      catch (OperationCanceledException) {
        throw new TransportException(cancellationToken.IsCancellationRequested
          ? TransportErrorKind.Cancelled
          : TransportErrorKind.Timeout);
      }
    }
  }
}
