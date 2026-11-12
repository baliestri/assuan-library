// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace AssuanLibrary.Client;

/// <summary>Explicitly owned protected data; status and comments remain ordinary memory.</summary>
public sealed class SecretCollectedResponse : IDisposable {
  private readonly SecretBuffer _data;

  internal SecretCollectedResponse(SecretBuffer data, IReadOnlyList<CollectedStatus> statuses,
    IReadOnlyList<byte[]> comments) {
    _data = data;
    Statuses = statuses;
    Comments = comments;
  }

  /// <summary>Copied status fields in ordinary memory, not protected by this object's disposal.</summary>
  public IReadOnlyList<CollectedStatus> Statuses { get; }

  /// <summary>Copied comments in ordinary memory, not protected by this object's disposal.</summary>
  public IReadOnlyList<byte[]> Comments { get; }

  /// <summary>Wipes the fixed-capacity data allocation.</summary>
  public void Dispose() => _data.Dispose();

  /// <summary>Explicitly borrows retained secret data until disposal.</summary>
  public ReadOnlySpan<byte> ExposeData() => _data.Expose();

  /// <inheritdoc />
  public override string ToString()
    => $"SecretCollectedResponse {{ StatusCount = {Statuses.Count}, CommentCount = {Comments.Count} }}";
}
