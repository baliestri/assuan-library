// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   Represents the method that will handle an inquire request on server side.
/// </summary>
public delegate void InquireHandler(IServerInquireContext context);
