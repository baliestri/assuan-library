// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport.IO;

namespace AssuanLibrary.Transport;

/// <summary>
///   Options for configuring an Assuan connection.
/// </summary>
public sealed class AssuanConnectionOptions {
  /// <summary>
  ///   A read-only instance of <see cref="AssuanConnectionOptions" /> with default settings.
  /// </summary>
  public static readonly AssuanConnectionOptions Default = new() {
    TimeoutInMilliseconds = 30_000,
    ConfigureStabilization = null
  };

  /// <summary>
  ///   The timeout duration for receiving data in milliseconds.
  /// </summary>
  public int TimeoutInMilliseconds { get; set; }

  /// <summary>
  ///   Configures the stabilization options for the connection.
  /// </summary>
  public Action<StabilizationOptions>? ConfigureStabilization { get; set; }
}
