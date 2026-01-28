// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Client.Abstractions;

/// <summary>
///   Represents the context of an inquire request on the client side.
/// </summary>
public interface IClientInquireContext {
  /// <summary>
  ///   The keyword of the inquire request.
  /// </summary>
  string Keyword { get; }

  /// <summary>
  ///   The parameters associated with the inquire request.
  /// </summary>
  IReadOnlyCollection<string> Parameters { get; }

  /// <summary>
  ///   Writes a data string response to the inquire request.
  /// </summary>
  /// <param name="value">The data string to write.</param>
  void Write(string value);

  /// <summary>
  ///   Writes a data buffer response to the inquire request.
  /// </summary>
  /// <param name="buffer">The data buffer to write.</param>
  void Write(byte[] buffer);

  /// <summary>
  ///   Ends the inquire request.
  /// </summary>
  void End();

  /// <summary>
  ///   Cancels the inquire request.
  /// </summary>
  void Cancel();

  /// <summary>
  ///   Writes a data string response to the inquire request.
  /// </summary>
  /// <param name="value">The data string to write.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous write operation.</returns>
  ValueTask WriteAsync(string value, CancellationToken ct = default);

  /// <summary>
  ///   Writes a data buffer response to the inquire request.
  /// </summary>
  /// <param name="buffer">The data buffer to write.</param>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous write operation.</returns>
  ValueTask WriteAsync(byte[] buffer, CancellationToken ct = default);

  /// <summary>
  ///   Ends the inquire request.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous end operation.</returns>
  ValueTask EndAsync(CancellationToken ct = default);

  /// <summary>
  ///   Cancels the inquire request.
  /// </summary>
  /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
  /// <returns>A value task that represents the asynchronous cancel operation.</returns>
  ValueTask CancelAsync(CancellationToken ct = default);
}
