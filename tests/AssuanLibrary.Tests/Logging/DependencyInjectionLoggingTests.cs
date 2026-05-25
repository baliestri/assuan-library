// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Client;
using AssuanLibrary.Extensions.DependencyInjection;
using AssuanLibrary.Logging;
using AssuanLibrary.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssuanLibrary.Tests.Logging;

public sealed class DependencyInjectionLoggingTests {
  [Fact]
  public void AddAssuanClient_ShouldUseILoggerFactory_WhenRegistered() {
    var loggerFactory = new RecordingLoggerFactory();
    var services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(loggerFactory);
    services.AddAssuanClient(null);

    using var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<AssuanClientOptions>();

    options.Logging.Logger.ShouldNotBeSameAs(NullAssuanLogger.Instance);

    options.Logging.Logger.Log(
      AssuanLogLevel.Debug,
      new AssuanEventId(42, "test"),
      "hello",
      null,
      static (state, _) => state
    );

    loggerFactory.CategoryName.ShouldBe("AssuanLibrary.Client");
    loggerFactory.Logger.Messages.ShouldContain("hello");
  }

  [Fact]
  public void AddAssuanServer_ShouldKeepNullLogger_WhenILoggerFactoryIsNotRegistered() {
    var services = new ServiceCollection();
    services.AddAssuanServer(null);

    using var serviceProvider = services.BuildServiceProvider();
    var options = serviceProvider.GetRequiredService<AssuanServerOptions>();

    options.Logging.Logger.ShouldBeSameAs(NullAssuanLogger.Instance);
  }

  private sealed class RecordingLoggerFactory : ILoggerFactory {
    public RecordingMicrosoftLogger Logger { get; } = new();

    public string? CategoryName { get; private set; }

    public ILogger CreateLogger(string categoryName) {
      CategoryName = categoryName;
      return Logger;
    }

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }
  }

  private sealed class RecordingMicrosoftLogger : ILogger {
    public List<string> Messages { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
      => null;

    public bool IsEnabled(LogLevel logLevel)
      => logLevel is not LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
      => Messages.Add(formatter(state, exception));
  }
}
