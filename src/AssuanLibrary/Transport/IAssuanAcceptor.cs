// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport;

/// <summary>Asynchronously admits owned duplex streams, independent of Assuan session processing.</summary>
/// <remarks>Custom backends own their admission policy and must document cancellation and disposal.</remarks>
public interface IAssuanAcceptor : IAsyncDisposable {
  /// <summary>Accepts one connected stream; the caller owns the returned stream.</summary>
  ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default);
}
