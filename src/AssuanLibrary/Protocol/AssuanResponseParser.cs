// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

internal sealed class AssuanResponseParser {
  public AssuanResponseCollection Parse(byte[] buffer)
    => new(buffer);

  public AssuanResponseCollection Parse(ReadOnlyMemory<byte> buffer)
    => new(buffer);
}

