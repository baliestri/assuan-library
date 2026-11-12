// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server;

/// <summary>How a peer terminated one inquiry; CAN does not decide the command's final response.</summary>
public enum InquiryOutcome {
  /// <summary>The peer sent END.</summary>
  Completed,

  /// <summary>The peer sent CAN; the handler still decides its final response.</summary>
  Cancelled
}
