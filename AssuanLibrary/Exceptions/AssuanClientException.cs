// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   Represents errors that occur during Assuan TCP client operations.
/// </summary>
public sealed class AssuanClientException : Exception {
  /// <inheritdoc />
  public AssuanClientException(string message) : base(message) { }

  /// <inheritdoc />
  public AssuanClientException(string message, Exception innerException) : base(message, innerException) { }
}
