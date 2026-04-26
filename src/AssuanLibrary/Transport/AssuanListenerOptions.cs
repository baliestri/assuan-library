// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Transport;

/// <summary>
///   Options for configuring an Assuan listener.
/// </summary>
public sealed class AssuanListenerOptions {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanListenerOptions" /> with default settings.
  /// </summary>
  public static readonly AssuanListenerOptions Default = new();

  /// <summary>
  ///   Configures the stabilization options for the listener.
  /// </summary>
  public Action<StabilizationOptions>? ConfigureStabilization { get; set; }
}
