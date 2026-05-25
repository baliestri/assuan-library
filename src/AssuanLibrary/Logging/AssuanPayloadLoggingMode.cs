// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Logging;

/// <summary>
///   Defines how Assuan message payloads are included in logs.
/// </summary>
public enum AssuanPayloadLoggingMode {
  /// <summary>
  ///   Does not include message payloads in logs.
  /// </summary>
  None,

  /// <summary>
  ///   Includes payload presence and size while hiding the actual content.
  /// </summary>
  Redacted,

  /// <summary>
  ///   Includes raw decoded payload content.
  /// </summary>
  Raw
}
