// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   Represents the context of an inquire request on the server side.
/// </summary>
public interface IServerInquireContext : IDisposable {
  /// <summary>
  ///   The keyword of the inquire request.
  /// </summary>
  string Keyword { get; }

  /// <summary>
  ///   The parameters associated with the inquire request.
  /// </summary>
  IReadOnlyCollection<string> Parameters { get; }

  /// <summary>
  ///   Waits for data to be written to the inquire request.
  /// </summary>
  /// <returns>The data written to the inquire request.</returns>
  byte[] Wait();

  /// <summary>
  ///   Waits for data to be written to the inquire request asynchronously.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>The data written to the inquire request.</returns>
  ValueTask<ReadOnlyMemory<byte>> WaitAsync(CancellationToken ct = default);
}
