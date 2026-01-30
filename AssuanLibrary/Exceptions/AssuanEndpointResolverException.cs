// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Exceptions;

/// <summary>
///   Represents errors that occur during the resolution of Assuan endpoints.
/// </summary>
public class AssuanEndpointResolverException : Exception {
  /// <inheritdoc />
  public AssuanEndpointResolverException(string message) : base(message) { }

  /// <inheritdoc />
  public AssuanEndpointResolverException(string message, Exception innerException) : base(message, innerException) { }
}
