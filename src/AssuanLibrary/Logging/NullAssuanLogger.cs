// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Logging;

/// <summary>
///   Represents an Assuan logger that discards every log entry.
/// </summary>
public sealed class NullAssuanLogger : IAssuanLogger {
  /// <summary>
  ///   Gets the shared null logger instance.
  /// </summary>
  public static readonly NullAssuanLogger Instance = new();

  private NullAssuanLogger() { }

  /// <inheritdoc />
  public IDisposable BeginScope<TState>(TState state) where TState : notnull
    => NullScope.Instance;

  /// <inheritdoc />
  public bool IsEnabled(AssuanLogLevel logLevel)
    => false;

  /// <inheritdoc />
  public void Log<TState>(
    AssuanLogLevel logLevel,
    AssuanEventId eventId,
    TState state,
    Exception? exception,
    Func<TState, Exception?, string> formatter
  ) { }

  private sealed class NullScope : IDisposable {
    public static readonly NullScope Instance = new();

    private NullScope() { }

    public void Dispose() { }
  }
}
