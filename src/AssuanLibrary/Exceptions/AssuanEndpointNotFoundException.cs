// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   The exception that is thrown when an Assuan endpoint cannot be found.
/// </summary>
public sealed class AssuanEndpointNotFoundException : AssuanEndpointResolverException {
  /// <inheritdoc />
  public AssuanEndpointNotFoundException(string message) : base(message) { }

  /// <inheritdoc />
  public AssuanEndpointNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
