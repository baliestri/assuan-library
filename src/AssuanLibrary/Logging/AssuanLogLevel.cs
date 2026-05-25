// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Logging;

/// <summary>
///   Defines the severity level of an Assuan log entry.
/// </summary>
public enum AssuanLogLevel {
  /// <summary>
  ///   Logs that contain the most detailed messages.
  /// </summary>
  Trace,

  /// <summary>
  ///   Logs that are useful during interactive investigation.
  /// </summary>
  Debug,

  /// <summary>
  ///   Logs that track the general flow of the application.
  /// </summary>
  Information,

  /// <summary>
  ///   Logs that highlight abnormal or unexpected events.
  /// </summary>
  Warning,

  /// <summary>
  ///   Logs that highlight failures in the current flow.
  /// </summary>
  Error,

  /// <summary>
  ///   Logs that describe unrecoverable failures.
  /// </summary>
  Critical,

  /// <summary>
  ///   Logging is disabled.
  /// </summary>
  None
}
