// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol.State;

/// <summary>Pure server phase validation; the caller must invalidate after a failed send.</summary>
internal sealed class ServerMachine {
  private ServerState _inquiryOrigin = ServerState.Greeting;

  internal ServerState State { get; private set; } = ServerState.Greeting;

  internal void ReceiveRequest(RequestKind kind) {
    if (State is ServerState.Invalid or ServerState.Closed) {
      throw new StateException(StateErrorKind.Unusable);
    }

    if (kind is RequestKind.Comment or RequestKind.Empty ||
        (State == ServerState.Inquiry && kind == RequestKind.Data)) {
      return;
    }

    switch (State) {
      case ServerState.Ready when
        kind == RequestKind.Command:
        State = ServerState.Handler;
        return;
      case ServerState.Inquiry when
        kind is RequestKind.End or RequestKind.Can:
        State = kind == RequestKind.End ? _inquiryOrigin : ServerState.AwaitFinal;
        return;
      default:
        Invalidate();
        throw new StateException(StateErrorKind.UnexpectedEvent);
    }
  }

  internal void SendResponse(ProtocolResponseKind kind) {
    if (State is ServerState.Invalid or ServerState.Closed) {
      throw new StateException(StateErrorKind.Unusable);
    }

    if (kind is ProtocolResponseKind.Comment or ProtocolResponseKind.Empty ||
        (State is ServerState.Greeting or ServerState.Handler or ServerState.Inquiry or ServerState.AwaitFinal &&
         kind == ProtocolResponseKind.Status) ||
        (State == ServerState.Handler && kind is ProtocolResponseKind.Data or ProtocolResponseKind.End)) {
      return;
    }

    switch (State) {
      case ServerState.Greeting or ServerState.Handler when
        kind == ProtocolResponseKind.Inquire:
        _inquiryOrigin = State;
        State = ServerState.Inquiry;
        return;
      case ServerState.Greeting when kind == ProtocolResponseKind.Ok:
      case ServerState.Handler when kind is ProtocolResponseKind.Ok or ProtocolResponseKind.Err:
        State = ServerState.Ready;
        return;
      case ServerState.Greeting when kind == ProtocolResponseKind.Err:
      case ServerState.AwaitFinal when _inquiryOrigin == ServerState.Greeting && kind == ProtocolResponseKind.Err:
        Invalidate();
        return;
      case ServerState.AwaitFinal when
        kind is ProtocolResponseKind.Ok or ProtocolResponseKind.Err:
        State = ServerState.Ready;
        return;
      default:
        Invalidate();
        throw new StateException(StateErrorKind.UnexpectedEvent);
    }
  }

  internal void Invalidate() {
    if (State != ServerState.Closed) {
      State = ServerState.Invalid;
    }
  }

  internal void Close()
    => State = ServerState.Closed;
}
