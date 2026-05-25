// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using AssuanLibrary.Platform.Common.Transport.Endpoints;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Tests.Client.Fakes;

internal sealed class FakeEndpointResolver : IAssuanEndpointResolver {
  public AssuanEndpointKind? LastKind { get; private set; }

  public AssuanEndpointResolution Resolution { get; set; }
    = new(new TcpClientEndpoint(IPAddress.Loopback, 0), new Dictionary<string, object>());

  public AssuanEndpointResolution Resolve(AssuanEndpointKind kind) {
    LastKind = kind;
    return Resolution;
  }
}
