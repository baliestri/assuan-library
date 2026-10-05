// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

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

/// <summary>Explicitly owned protected data; status and comments remain ordinary memory.</summary>
public sealed class SecretCollectedResponse : IDisposable {
  private readonly SecretBuffer _data;

  internal SecretCollectedResponse(SecretBuffer data, IReadOnlyList<CollectedStatus> statuses, IReadOnlyList<byte[]> comments) {
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
