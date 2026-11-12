// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client;

/// <summary>Owned public status fields copied from the wire.</summary>
public sealed class CollectedStatus {
  internal CollectedStatus(byte[] keyword, byte[] arguments) {
    KeywordBytes = keyword;
    Arguments = arguments;
  }

  /// <summary>Validated ASCII keyword copied to ordinary memory.</summary>
  public ReadOnlyMemory<byte> KeywordBytes { get; }

  /// <summary>Original status argument bytes copied to ordinary memory.</summary>
  public ReadOnlyMemory<byte> Arguments { get; }

  /// <inheritdoc />
  public override string ToString()
    => $"CollectedStatus {{ KeywordLength = {KeywordBytes.Length}, ArgumentsLength = {Arguments.Length} }}";
}
