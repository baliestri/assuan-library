// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Transport;

/// <summary>
///   Options for configuring an Assuan listener.
/// </summary>
public sealed class AssuanListenerOptions {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanListenerOptions" /> with default settings.
  /// </summary>
  public static readonly AssuanListenerOptions Default = new() {
    Banner = "Assuan Server Ready"
  };

  /// <summary>
  ///   The banner message to send upon connection establishment.
  /// </summary>
  public string? Banner { get; set; }

  /// <summary>
  ///   Indicates whether to send the banner message upon connection establishment.
  /// </summary>
  [MemberNotNullWhen(true, nameof(Banner))]
  public bool SendBanner => !string.IsNullOrWhiteSpace(Banner);

  /// <summary>
  ///   Configures the stabilization options for the listener.
  /// </summary>
  public Action<StabilizationOptions>? ConfigureStabilization { get; set; }
}
