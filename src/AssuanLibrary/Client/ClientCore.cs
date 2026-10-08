// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Diagnostics;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;
using AssuanLibrary.Transport;

namespace AssuanLibrary.Client;

/// <summary>Owned channel and protocol state shared by one exclusive transaction at a time.</summary>
internal sealed class ClientCore(Stream stream, TimeProvider? clock = null, IAssuanEventSink? eventSink = null) {
  internal bool GreetingPending = true;
  internal bool IoUncertain;
  internal int TransactionActive;
  internal TimeProvider Clock { get; } = clock ?? TimeProvider.System;
  internal AssuanChannel Channel { get; } = new(stream, eventSink);
  internal IAssuanEventSink? EventSink { get; } = eventSink;
  internal ClientMachine Machine { get; } = new();
  internal bool IsUsable => !IoUncertain && Machine.State == ClientState.Ready;

  internal void Invalidate() {
    IoUncertain = true;
    Machine.Invalidate();
    Channel.Close();
  }

  // One event classifier shared by command transactions and manual greetings.
  internal async Task<ClientEventLease> ReadEventAsync(TransportDeadline deadline, ClientOptions options,
  PayloadSensitivity sensitivity, Action release, CancellationToken token) {
    try {
      while (true) {
        if (deadline.Remaining == TimeSpan.Zero) {
          throw new ClientException(ClientErrorKind.Timeout);
        }

        IoUncertain = true;
        await Channel.ReadLineAsync(deadline.Remaining, token).ConfigureAwait(false);
        ProtocolResponseKind wireKind;
        int offset = 0, length = 0, keywordLength = 0;
        uint? remoteCode = null;
        {
          var line = Channel.Line;
          var parsed = ProtocolResponse.Parse(line);
          wireKind = parsed.Kind;
          switch (wireKind) {
            case ProtocolResponseKind.Data or ProtocolResponseKind.Comment or ProtocolResponseKind.Ok or ProtocolResponseKind.Err:
              length = parsed.PayloadBytes.Length;
              offset = wireKind switch {
                ProtocolResponseKind.Data => 2,
                ProtocolResponseKind.Comment => 1,
                var _ => line.Length - length
              };
              break;
            case ProtocolResponseKind.Status:
              keywordLength = parsed.KeywordBytes.Length;
              length = parsed.Arguments.Length;
              offset = line.Length - length;
              break;
            case ProtocolResponseKind.Inquire:
              keywordLength = parsed.KeywordBytes.Length;
              length = parsed.Arguments.Length;
              break;
          }

          if (wireKind == ProtocolResponseKind.Err) {
            remoteCode = parsed.ErrorCode;
          }
        }

        if (wireKind == ProtocolResponseKind.Empty) {
          IoUncertain = false;
          continue;
        }

        if (token.IsCancellationRequested) {
          throw new ClientException(ClientErrorKind.Cancelled);
        }

        if (deadline.Remaining == TimeSpan.Zero) {
          throw new ClientException(ClientErrorKind.Timeout);
        }

        // A greeting ERR is observable as a final event; Finish rejects it.
        if (!(GreetingPending && wireKind == ProtocolResponseKind.Err)) {
          Machine.Receive(wireKind);
        }

        ClientInquiry? inquiry = null;
        if (wireKind == ProtocolResponseKind.Inquire) {
          var line = Channel.Line;
          inquiry = new ClientInquiry(this, deadline, options, sensitivity,
            line.Slice(8, keywordLength), line[^length..]);
        }

        var kind = wireKind switch {
          ProtocolResponseKind.Data => ClientEventKind.Data,
          ProtocolResponseKind.Status => ClientEventKind.Status,
          ProtocolResponseKind.Comment => ClientEventKind.Comment,
          ProtocolResponseKind.End => ClientEventKind.End,
          ProtocolResponseKind.Inquire => ClientEventKind.Inquire,
          ProtocolResponseKind.Ok or ProtocolResponseKind.Err => ClientEventKind.Finished,
          var _ => throw new ClientException(ClientErrorKind.Protocol)
        };

        if (kind == ClientEventKind.Data) {
          length = ProtocolEscaping.DecodeDataInPlace(Channel.MutableLine[2..]);
        }

        var received = Channel.ReceivedLineMemory;
        var payload = kind == ClientEventKind.Inquire ? ReadOnlyMemory<byte>.Empty : received.Slice(offset, length);
        var keyword = kind == ClientEventKind.Status ? received.Slice(2, keywordLength) : ReadOnlyMemory<byte>.Empty;
        Channel.BeginReceivedLease();
        IoUncertain = false;
        return new ClientEventLease(kind, payload, keyword, sensitivity, remoteCode, release, inquiry);
      }
    }
    catch (Exception error) {
      Invalidate();
      throw Classify(error, token);
    }
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
