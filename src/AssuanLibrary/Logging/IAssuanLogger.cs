// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Logging;

/// <summary>
///   Represents a logger compatible with the shape of Microsoft.Extensions.Logging.ILogger without requiring that package.
/// </summary>
public interface IAssuanLogger {
  /// <summary>
  ///   Begins a logical operation scope.
  /// </summary>
  /// <param name="state">The scope state.</param>
  /// <typeparam name="TState">The type of the scope state.</typeparam>
  /// <returns>An object that ends the scope when disposed.</returns>
  IDisposable? BeginScope<TState>(TState state) where TState : notnull;

  /// <summary>
  ///   Checks whether the given log level is enabled.
  /// </summary>
  /// <param name="logLevel">The log level.</param>
  /// <returns><see langword="true" /> if logging is enabled for the level; otherwise, <see langword="false" />.</returns>
  bool IsEnabled(AssuanLogLevel logLevel);

  /// <summary>
  ///   Writes a log entry.
  /// </summary>
  /// <param name="logLevel">The log level.</param>
  /// <param name="eventId">The log event identifier.</param>
  /// <param name="state">The log state.</param>
  /// <param name="exception">The exception related to the entry, if any.</param>
  /// <param name="formatter">The formatter that creates the message from state and exception.</param>
  /// <typeparam name="TState">The type of the log state.</typeparam>
  void Log<TState>(
    AssuanLogLevel logLevel,
    AssuanEventId eventId,
    TState state,
    Exception? exception,
    Func<TState, Exception?, string> formatter
  );
}
