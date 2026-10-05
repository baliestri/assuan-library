// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol.State;

/// <summary>Pure client phase validation; the caller must invalidate after uncertain I/O.</summary>
internal sealed class ClientMachine {
  private ClientState _inquiryOrigin = ClientState.Greeting;

  internal ClientState State { get; private set; } = ClientState.Greeting;

  internal void BeginCommand() {
    if (State == ClientState.Ready) {
      State = ClientState.Command;
    }
    else {
      throw new StateException(State is ClientState.Invalid or ClientState.Closed
        ? StateErrorKind.Unusable
        : StateErrorKind.NotReady);
    }
  }

  internal void Receive(ProtocolResponseKind kind) {
    if (State is ClientState.Invalid or ClientState.Closed) {
      throw new StateException(StateErrorKind.Unusable);
    }

    if (kind is ProtocolResponseKind.Comment or ProtocolResponseKind.Empty ||
        (State is ClientState.Greeting or ClientState.Command or ClientState.Inquiry or ClientState.AwaitFinal &&
         kind == ProtocolResponseKind.Status) ||
        (State == ClientState.Command && kind is ProtocolResponseKind.Data or ProtocolResponseKind.End)) {
      return;
    }

    switch (State) {
      case ClientState.Greeting or ClientState.Command when
        kind == ProtocolResponseKind.Inquire:
        _inquiryOrigin = State;
        State = ClientState.Inquiry;
        return;
      case ClientState.Greeting when kind == ProtocolResponseKind.Ok:
      case ClientState.Command when kind is ProtocolResponseKind.Ok or ProtocolResponseKind.Err:
        State = ClientState.Ready;
        return;
      case ClientState.Greeting when kind == ProtocolResponseKind.Err:
      case ClientState.AwaitFinal when _inquiryOrigin == ClientState.Greeting && kind == ProtocolResponseKind.Err:
        Invalidate();
        throw new StateException(StateErrorKind.GreetingRejected);
      case ClientState.AwaitFinal when
        kind is ProtocolResponseKind.Ok or ProtocolResponseKind.Err:
        State = ClientState.Ready;
        return;
      default:
        Invalidate();
        throw new StateException(StateErrorKind.UnexpectedEvent);
    }
  }

  internal void FinishInquiry(bool cancelled) {
    if (State is ClientState.Invalid or ClientState.Closed) {
      throw new StateException(StateErrorKind.Unusable);
    }

    if (State != ClientState.Inquiry) {
      throw new StateException(StateErrorKind.NotReady);
    }

    State = cancelled ? ClientState.AwaitFinal : _inquiryOrigin;
  }

  internal void Invalidate() {
    if (State != ClientState.Closed) {
      State = ClientState.Invalid;
    }
  }

  internal void Close()
    => State = ClientState.Closed;
}
