// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Network;

/// <summary>
///   Represents errors that occur during Assuan TCP client operations.
/// </summary>
public sealed class AssuanTcpClientException : Exception {
  /// <inheritdoc />
  public AssuanTcpClientException(string message) : base(message) { }

  /// <inheritdoc />
  public AssuanTcpClientException(string message, Exception innerException) : base(message, innerException) { }
}
