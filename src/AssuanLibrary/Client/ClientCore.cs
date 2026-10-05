// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>Owned channel and protocol state shared by one exclusive transaction at a time.</summary>
internal sealed class ClientCore(Stream stream, TimeProvider? clock = null) {
  internal bool IoUncertain;
  internal int TransactionActive;
  internal TimeProvider Clock { get; } = clock ?? TimeProvider.System;
  internal AssuanChannel Channel { get; } = new(stream);
  internal ClientMachine Machine { get; } = new();
  internal bool IsUsable => !IoUncertain && Machine.State == ClientState.Ready;

  internal void Invalidate() {
    IoUncertain = true;
    Machine.Invalidate();
    Channel.Close();
  }

  internal static ClientException Classify(Exception error, CancellationToken token)
    => error switch {
      ClientException known => known,
      TransportException { Kind: TransportErrorKind.Timeout } => new ClientException(ClientErrorKind.Timeout, technicalCause: error),
      TransportException { Kind: TransportErrorKind.Cancelled } => new ClientException(ClientErrorKind.Cancelled, technicalCause: error),
      TransportException { Kind: TransportErrorKind.InvalidEndpoint or TransportErrorKind.InvalidOptions }
        => new ClientException(ClientErrorKind.InvalidOptions, technicalCause: error),
      TransportException { Kind: TransportErrorKind.Framing }
        => new ClientException(ClientErrorKind.Protocol, technicalCause: error),
      TransportException { Kind: TransportErrorKind.Closed }
        => new ClientException(ClientErrorKind.State, technicalCause: error),
      TransportException => new ClientException(ClientErrorKind.Io, technicalCause: error),
      ProtocolException => new ClientException(ClientErrorKind.Protocol, technicalCause: error),
      StateException => new ClientException(ClientErrorKind.State, technicalCause: error),
      OperationCanceledException => new ClientException(token.IsCancellationRequested
        ? ClientErrorKind.Cancelled
        : ClientErrorKind.Timeout, technicalCause: error),
      TimeoutException => new ClientException(ClientErrorKind.Timeout, technicalCause: error),
      IOException => new ClientException(ClientErrorKind.Io, technicalCause: error),
      var _ => new ClientException(ClientErrorKind.State, technicalCause: error)
    };
}
