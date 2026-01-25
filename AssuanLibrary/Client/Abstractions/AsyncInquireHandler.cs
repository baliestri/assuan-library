// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client.Abstractions;

/// <summary>
///   Represents the asynchronous method that will handle an inquire request.
/// </summary>
public delegate Task AsyncInquireHandler(IInquireContext context, CancellationToken ct = default);
