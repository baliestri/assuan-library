// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Sample;

public readonly record struct ConsoleEndpoint : IAssuanEndpoint {
  public static readonly ConsoleEndpoint Instance = new();
}
