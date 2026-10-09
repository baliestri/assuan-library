// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Diagnostics;

/// <summary>Which layer reports a payload-free operation result.</summary>
public enum AssuanComponent {
  /// <summary>A server-session operation.</summary>
  Server,

  /// <summary>A client transaction.</summary>
  Client,

  /// <summary>Underlying channel I/O.</summary>
  Transport
}

/// <summary>Stable IDs for safe lifecycle observations.</summary>
public enum AssuanEventId {
  /// <summary>A server final response was written.</summary>
  FinalResponse = 1,

  /// <summary>A client observed a remote command ERR.</summary>
  RemoteFinal = 2,

  /// <summary>Channel read or framing failed.</summary>
  ReadFailed = 3,

  /// <summary>Channel write or flush failed.</summary>
  WriteFailed = 4
}

/// <summary>Safe category of one completed operation.</summary>
public enum AssuanOutcome {
  /// <summary>Operation completed successfully.</summary>
  Success,

  /// <summary>A final remote error was observed or sent.</summary>
  RemoteError,

  /// <summary>The deadline expired.</summary>
  Timeout,

  /// <summary>The caller cancelled the operation.</summary>
  Cancelled,

  /// <summary>An unrelated transport or framing failure occurred.</summary>
  Failure
}

/// <summary>Only enums and a nonnegative count; never holds protocol or exception data.</summary>
public readonly struct AssuanEvent {
  /// <summary>Creates a payload-free result event.</summary>
  public AssuanEvent(AssuanComponent component, AssuanEventId id, AssuanOutcome outcome, int byteCount = 0) {
    ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
    Component = component;
    Id = id;
    Outcome = outcome;
    ByteCount = byteCount;
  }

  /// <summary>Reporting layer.</summary>
  public AssuanComponent Component { get; }

  /// <summary>Stable operation ID.</summary>
  public AssuanEventId Id { get; }

  /// <summary>Safe operation result.</summary>
  public AssuanOutcome Outcome { get; }

  /// <summary>Optional count of complete wire bytes known by the caller.</summary>
  public int ByteCount { get; }

  /// <inheritdoc />
  public override string ToString()
    => $"AssuanEvent {{ Component = {Component}, Id = {Id}, Outcome = {Outcome}, ByteCount = {ByteCount} }}";
}
