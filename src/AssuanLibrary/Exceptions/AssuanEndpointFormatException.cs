// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   The exception that is thrown when an Assuan endpoint has an invalid format.
/// </summary>
public sealed class AssuanEndpointFormatException : AssuanEndpointResolverException {
  /// <inheritdoc />
  public AssuanEndpointFormatException(string message) : base(message) { }

  /// <inheritdoc />
  public AssuanEndpointFormatException(string message, Exception innerException) : base(message, innerException) { }
}
