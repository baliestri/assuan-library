// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Endpoints;

namespace AssuanLibrary.Transport;

public interface IAssuanConnectionFactory {
  IAssuanConnection Create(IAssuanEndpoint endpoint);
}
