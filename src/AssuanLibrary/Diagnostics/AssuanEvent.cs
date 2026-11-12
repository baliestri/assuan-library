// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace AssuanLibrary.Diagnostics;

/// <summary>Only enums and a nonnegative count; never holds protocol or exception data.</summary>
[StructLayout(LayoutKind.Sequential)]
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
  [SuppressMessage("ReSharper", "MemberCanBePrivate.Global",
    Justification = "Public event field read by consumer sinks.")]
  public int ByteCount { get; }

  /// <inheritdoc />
  public override string ToString()
    => $"AssuanEvent {{ Component = {Component}, Id = {Id}, Outcome = {Outcome}, ByteCount = {ByteCount} }}";
}
