// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol.State;

internal enum ClientState { Greeting, Ready, Command, Inquiry, AwaitFinal, Invalid, Closed }

internal enum ServerState { Greeting, Ready, Handler, Inquiry, AwaitFinal, Invalid, Closed }

internal enum RequestKind { Command, Data, End, Can, Comment, Empty }

internal enum StateErrorKind { UnexpectedEvent, NotReady, Unusable, GreetingRejected }

internal sealed class StateException : Exception {
  internal StateException(StateErrorKind kind) : base(kind switch {
    StateErrorKind.UnexpectedEvent => "unexpected session event",
    StateErrorKind.NotReady => "session is not ready for this operation",
    StateErrorKind.Unusable => "session is invalid or closed",
    StateErrorKind.GreetingRejected => "server rejected the greeting",
    var _ => "invalid session state"
  }) => Kind = kind;

  internal StateErrorKind Kind { get; }
}
