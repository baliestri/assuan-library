// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Logging;

internal static class AssuanLogEvents {
  public static readonly AssuanEventId ConnectionOpened = new(1, nameof(ConnectionOpened));
  public static readonly AssuanEventId ConnectionClosed = new(2, nameof(ConnectionClosed));
  public static readonly AssuanEventId MessageSent = new(3, nameof(MessageSent));
  public static readonly AssuanEventId MessageReceived = new(4, nameof(MessageReceived));
}
