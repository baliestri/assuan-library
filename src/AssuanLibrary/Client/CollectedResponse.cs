// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client;

/// <summary>Opt-in public data and metadata held in ordinary managed arrays.</summary>
public sealed class CollectedResponse {
  internal CollectedResponse(byte[] data, IReadOnlyList<CollectedStatus> statuses, IReadOnlyList<byte[]> comments) {
    Data = data;
    Statuses = statuses;
    Comments = comments;
  }

  /// <summary>Concatenated decoded data; the caller owns this ordinary array.</summary>
  public byte[] Data { get; }

  /// <summary>Copied status keyword and argument bytes.</summary>
  public IReadOnlyList<CollectedStatus> Statuses { get; }

  /// <summary>Copied bytes following each comment marker.</summary>
  public IReadOnlyList<byte[]> Comments { get; }

  /// <inheritdoc />
  public override string ToString()
    => $"CollectedResponse {{ DataLength = {Data.Length}, StatusCount = {Statuses.Count}, CommentCount = {Comments.Count} }}";
}
