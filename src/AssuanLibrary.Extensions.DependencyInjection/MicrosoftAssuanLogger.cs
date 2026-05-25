// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Logging;
using Microsoft.Extensions.Logging;

namespace AssuanLibrary.Extensions.DependencyInjection;

internal sealed class MicrosoftAssuanLogger(ILogger logger) : IAssuanLogger {
  public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    => logger.BeginScope(state);

  public bool IsEnabled(AssuanLogLevel logLevel)
    => logger.IsEnabled(ToMicrosoftLogLevel(logLevel));

  public void Log<TState>(AssuanLogLevel logLevel, AssuanEventId eventId, TState state, Exception? exception,
  Func<TState, Exception?, string> formatter)
    => logger.Log(ToMicrosoftLogLevel(logLevel), ToMicrosoftEventId(eventId), state, exception, formatter);

  private static LogLevel ToMicrosoftLogLevel(AssuanLogLevel logLevel)
    => logLevel switch {
      AssuanLogLevel.Trace => LogLevel.Trace,
      AssuanLogLevel.Debug => LogLevel.Debug,
      AssuanLogLevel.Information => LogLevel.Information,
      AssuanLogLevel.Warning => LogLevel.Warning,
      AssuanLogLevel.Error => LogLevel.Error,
      AssuanLogLevel.Critical => LogLevel.Critical,
      AssuanLogLevel.None => LogLevel.None,
      var _ => LogLevel.None
    };

  private static EventId ToMicrosoftEventId(AssuanEventId eventId)
    => new(eventId.Id, eventId.Name);
}
