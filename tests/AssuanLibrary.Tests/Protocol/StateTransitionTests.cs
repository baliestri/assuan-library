// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.State;

namespace AssuanLibrary.Tests.Protocol;

public sealed class StateTransitionTests {
  private static readonly ProtocolResponseKind[] _responseKinds = [
    ProtocolResponseKind.Ok, ProtocolResponseKind.Err, ProtocolResponseKind.Data, ProtocolResponseKind.Status,
    ProtocolResponseKind.Inquire, ProtocolResponseKind.End, ProtocolResponseKind.Comment, ProtocolResponseKind.Empty
  ];

  [Fact]
  public void EveryClientPhaseEnforcesTheRustResponseLegalityMatrix() {
    (ClientState State, bool[] Accepted)[] cases = [
      (ClientState.Greeting, [true, false, false, true, true, false, true, true]),
      (ClientState.Ready, [false, false, false, false, false, false, true, true]),
      (ClientState.Command, [true, true, true, true, true, true, true, true]),
      (ClientState.Inquiry, [false, false, false, true, false, false, true, true]),
      (ClientState.AwaitFinal, [true, true, false, true, false, false, true, true]),
      (ClientState.Invalid, [false, false, false, false, false, false, false, false]),
      (ClientState.Closed, [false, false, false, false, false, false, false, false])
    ];
    foreach (var (state, accepted) in cases) {
      for (var index = 0; index < _responseKinds.Length; ++index) {
        var machine = ClientIn(state);
        var success = true;
        try { machine.Receive(_responseKinds[index]); }
        catch (StateException) { success = false; }

        Assert.Equal(accepted[index], success);
        if (!success) {
          Assert.Equal(state == ClientState.Closed ? ClientState.Closed : ClientState.Invalid, machine.State);
        }
      }
    }
  }

  [Fact]
  public void EveryServerPhaseEnforcesBothResponseAndRequestLegalityMatrices() {
    (ServerState State, bool[] Accepted)[] responses = [
      (ServerState.Greeting, [true, true, false, true, true, false, true, true]),
      (ServerState.Ready, [false, false, false, false, false, false, true, true]),
      (ServerState.Handler, [true, true, true, true, true, true, true, true]),
      (ServerState.Inquiry, [false, false, false, true, false, false, true, true]),
      (ServerState.AwaitFinal, [true, true, false, true, false, false, true, true]),
      (ServerState.Invalid, [false, false, false, false, false, false, false, false]),
      (ServerState.Closed, [false, false, false, false, false, false, false, false])
    ];
    foreach (var (state, accepted) in responses) {
      for (var index = 0; index < _responseKinds.Length; ++index) {
        var machine = ServerIn(state);
        var success = true;
        try { machine.SendResponse(_responseKinds[index]); }
        catch (StateException) { success = false; }

        Assert.Equal(accepted[index], success);
        if (!success) {
          Assert.Equal(state == ServerState.Closed ? ServerState.Closed : ServerState.Invalid, machine.State);
        }
      }
    }

    RequestKind[] requestKinds = [
      RequestKind.Command, RequestKind.Data, RequestKind.End,
      RequestKind.Can, RequestKind.Comment, RequestKind.Empty
    ];
    (ServerState State, bool[] Accepted)[] requests = [
      (ServerState.Greeting, [false, false, false, false, true, true]),
      (ServerState.Ready, [true, false, false, false, true, true]),
      (ServerState.Handler, [false, false, false, false, true, true]),
      (ServerState.Inquiry, [false, true, true, true, true, true]),
      (ServerState.AwaitFinal, [false, false, false, false, true, true]),
      (ServerState.Invalid, [false, false, false, false, false, false]),
      (ServerState.Closed, [false, false, false, false, false, false])
    ];
    foreach (var (state, accepted) in requests) {
      for (var index = 0; index < requestKinds.Length; ++index) {
        var machine = ServerIn(state);
        var success = true;
        try { machine.ReceiveRequest(requestKinds[index]); }
        catch (StateException) { success = false; }

        Assert.Equal(accepted[index], success);
        if (!success) {
          Assert.Equal(state == ServerState.Closed ? ServerState.Closed : ServerState.Invalid, machine.State);
        }
      }
    }
  }

  private static ClientMachine ClientIn(ClientState state) {
    var machine = new ClientMachine();
    if (state == ClientState.Closed) {
      machine.Close();
      return machine;
    }

    if (state == ClientState.Invalid) {
      machine.Invalidate();
      return machine;
    }

    if (state == ClientState.Greeting) {
      return machine;
    }

    machine.Receive(ProtocolResponseKind.Ok);
    if (state == ClientState.Ready) {
      return machine;
    }

    machine.BeginCommand();
    if (state == ClientState.Command) {
      return machine;
    }

    machine.Receive(ProtocolResponseKind.Inquire);
    if (state == ClientState.AwaitFinal) {
      machine.FinishInquiry(true);
    }

    return machine;
  }

  private static ServerMachine ServerIn(ServerState state) {
    var machine = new ServerMachine();
    switch (state) {
      case ServerState.Closed:
        machine.Close();
        return machine;
      case ServerState.Invalid:
        machine.Invalidate();
        return machine;
      case ServerState.Greeting:
        return machine;
    }

    machine.SendResponse(ProtocolResponseKind.Ok);
    if (state == ServerState.Ready) {
      return machine;
    }

    machine.ReceiveRequest(RequestKind.Command);
    if (state == ServerState.Handler) {
      return machine;
    }

    machine.SendResponse(ProtocolResponseKind.Inquire);
    if (state == ServerState.AwaitFinal) {
      machine.ReceiveRequest(RequestKind.Can);
    }

    return machine;
  }

  [Fact]
  public void ClientSupportsGreetingInquiryCommandInquiryAndReusableCommandErrors() {
    var client = new ClientMachine();
    Assert.Equal(ClientState.Greeting, client.State);
    Assert.Equal(StateErrorKind.NotReady, Assert.Throws<StateException>(client.BeginCommand).Kind);
    Assert.Equal(ClientState.Greeting, client.State);
    client.Receive(ProtocolResponseKind.Comment);
    client.Receive(ProtocolResponseKind.Empty);
    client.Receive(ProtocolResponseKind.Status);
    client.Receive(ProtocolResponseKind.Inquire);
    Assert.Equal(ClientState.Inquiry, client.State);
    client.FinishInquiry(false);
    Assert.Equal(ClientState.Greeting, client.State);
    client.Receive(ProtocolResponseKind.Ok);
    Assert.Equal(ClientState.Ready, client.State);

    client.BeginCommand();
    client.Receive(ProtocolResponseKind.Data);
    client.Receive(ProtocolResponseKind.End);
    Assert.Equal(ClientState.Command, client.State);
    client.Receive(ProtocolResponseKind.Inquire);
    client.FinishInquiry(true);
    Assert.Equal(ClientState.AwaitFinal, client.State);
    client.Receive(ProtocolResponseKind.Status);
    client.Receive(ProtocolResponseKind.Err);
    Assert.Equal(ClientState.Ready, client.State);
    client.BeginCommand();
    client.Receive(ProtocolResponseKind.Ok);
    Assert.Equal(ClientState.Ready, client.State);
  }

  [Fact]
  public void GreetingErrorAndUnexpectedEventsInvalidateTheClient() {
    var rejected = new ClientMachine();
    Assert.Equal(StateErrorKind.GreetingRejected,
      Assert.Throws<StateException>(() => rejected.Receive(ProtocolResponseKind.Err)).Kind);
    Assert.Equal(ClientState.Invalid, rejected.State);
    Assert.Equal(StateErrorKind.Unusable, Assert.Throws<StateException>(rejected.BeginCommand).Kind);

    var cancelledGreeting = new ClientMachine();
    cancelledGreeting.Receive(ProtocolResponseKind.Inquire);
    cancelledGreeting.FinishInquiry(true);
    Assert.Equal(StateErrorKind.GreetingRejected,
      Assert.Throws<StateException>(() => cancelledGreeting.Receive(ProtocolResponseKind.Err)).Kind);

    var unexpected = new ClientMachine();
    Assert.Equal(StateErrorKind.UnexpectedEvent,
      Assert.Throws<StateException>(() => unexpected.Receive(ProtocolResponseKind.Data)).Kind);
    Assert.Equal(ClientState.Invalid, unexpected.State);
    unexpected.Close();
    unexpected.Invalidate();
    Assert.Equal(ClientState.Closed, unexpected.State);
    Assert.Equal(StateErrorKind.Unusable,
      Assert.Throws<StateException>(() => unexpected.Receive(ProtocolResponseKind.Ok)).Kind);
  }

  [Fact]
  public void ServerAllowsOneFinalAndSeparatesInquiryTerminationFromCommandCompletion() {
    var server = new ServerMachine();
    Assert.Equal(ServerState.Greeting, server.State);
    server.SendResponse(ProtocolResponseKind.Comment);
    server.SendResponse(ProtocolResponseKind.Status);
    server.SendResponse(ProtocolResponseKind.Inquire);
    server.ReceiveRequest(RequestKind.Data);
    server.ReceiveRequest(RequestKind.End);
    Assert.Equal(ServerState.Greeting, server.State);
    server.SendResponse(ProtocolResponseKind.Ok);
    Assert.Equal(ServerState.Ready, server.State);
    server.ReceiveRequest(RequestKind.Command);
    server.SendResponse(ProtocolResponseKind.Data);
    server.SendResponse(ProtocolResponseKind.End);
    server.SendResponse(ProtocolResponseKind.Inquire);
    server.ReceiveRequest(RequestKind.Can);
    Assert.Equal(ServerState.AwaitFinal, server.State);
    server.SendResponse(ProtocolResponseKind.Status);
    server.SendResponse(ProtocolResponseKind.Err);
    Assert.Equal(ServerState.Ready, server.State);

    Assert.Equal(StateErrorKind.UnexpectedEvent,
      Assert.Throws<StateException>(() => server.SendResponse(ProtocolResponseKind.Ok)).Kind);
    Assert.Equal(ServerState.Invalid, server.State);
  }

  [Fact]
  public void ServerRejectsGreetingErrorAndDoesNotConfuseCancelCommandWithCan() {
    var server = new ServerMachine();
    server.SendResponse(ProtocolResponseKind.Inquire);
    server.ReceiveRequest(RequestKind.Can);
    server.SendResponse(ProtocolResponseKind.Err);
    Assert.Equal(ServerState.Invalid, server.State);

    var normal = new ServerMachine();
    normal.SendResponse(ProtocolResponseKind.Ok);
    Assert.True(ProtocolCommand.Parse("CANCEL"u8).NameBytes.SequenceEqual("CANCEL"u8));
    normal.ReceiveRequest(RequestKind.Command);
    Assert.Equal(ServerState.Handler, normal.State);
    normal.Invalidate();
    Assert.Equal(StateErrorKind.Unusable,
      Assert.Throws<StateException>(() => normal.ReceiveRequest(RequestKind.Command)).Kind);
    normal.Close();
    Assert.Equal(ServerState.Closed, normal.State);
  }
}
