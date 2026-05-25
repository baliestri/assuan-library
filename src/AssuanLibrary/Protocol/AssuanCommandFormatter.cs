// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

internal sealed class AssuanCommandFormatter {
  public byte[] Format(AssuanCommand command)
    => command.ToBytes();

  public ReadOnlyMemory<byte> FormatAsync(AssuanCommand command)
    => command.ToReadOnlyMemory();
}

