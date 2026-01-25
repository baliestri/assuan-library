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
    Timeout = TimeSpan.FromSeconds(TIMEOUT_IN_SECONDS),
    ThrowIfNotConnected = true,
    ConfigureStabilization = null
  };

  /// <summary>
  ///   The timeout duration for receiving data.
  /// </summary>
  public TimeSpan Timeout { get; set; }

  /// <summary>
  ///   Indicates whether to throw an exception if the client is not connected when attempting to send or receive data.
  /// </summary>
  public bool ThrowIfNotConnected { get; set; }

  /// <summary>
  ///   Configures the stabilization options for the connection.
  /// </summary>
  public Action<StabilizationOptions>? ConfigureStabilization { get; set; }
}
