// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

internal enum AssuanServerState {
  Stopped,
  Starting,
  Running,
  Stopping,
  Disposed
}

