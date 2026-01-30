// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;

namespace AssuanLibrary.Client.Abstractions;

/// <summary>
///   Represents the asynchronous method that will handle a client hook.
/// </summary>
public delegate Task AsyncClientHook(IAssuanConnection connection, CancellationToken ct = default);

/// <summary>
///   Represents the asynchronous method that will handle a client hook with context.
/// </summary>
/// <typeparam name="TContext">The type of the context parameter.</typeparam>
public delegate Task AsyncClientHook<in TContext>(IAssuanConnection connection, TContext context, CancellationToken ct = default);
