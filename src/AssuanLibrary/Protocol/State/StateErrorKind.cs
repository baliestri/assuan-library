// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol.State;

internal enum StateErrorKind {
  UnexpectedEvent,
  NotReady,
  Unusable,
  GreetingRejected
}
