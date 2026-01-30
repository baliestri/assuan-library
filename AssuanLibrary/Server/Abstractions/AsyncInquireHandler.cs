// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   Represents the asynchronous method that will handle an inquire request on server side.
/// </summary>
public delegate Task AsyncInquireHandler(IServerInquireContext context, CancellationToken ct = default);
