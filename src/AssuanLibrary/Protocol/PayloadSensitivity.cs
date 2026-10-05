// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Protocol;

/// <summary>Caller-selected handling policy for binary payloads.</summary>
public enum PayloadSensitivity {
  /// <summary>The caller permits ordinary payload handling.</summary>
  Public,

  /// <summary>The caller requires redacted diagnostics and explicit byte exposure.</summary>
  Secret
}
