// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using AssuanLibrary.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace AssuanLibrary.Generators.Tests;

public sealed class LoggingIntegrationTests {
  [Fact]
  public void GeneratedSinkCallsILoggerWithOnlySafeFieldsAndNoException() {
    var result = GeneratorHarness.Compile("""
                                          public static class Consumer {
                                            public static AssuanLibrary.Diagnostics.IAssuanEventSink Create(
                                              Microsoft.Extensions.Logging.ILogger logger) =>
                                              AssuanLibrary.Generated.AssuanLoggingExtensions.CreateEventSink(logger);
                                          }
                                          """, extraReferences: [MetadataReference.CreateFromFile(typeof(ILogger).Assembly.Location)]);
    Assert.Empty(result.OutputCompilationErrors);
    using var image = new MemoryStream();
    var emitted = result.OutputCompilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
    var assembly = Assembly.Load(image.ToArray());
    var logger = new RecordingLogger();
    var sink = Assert.IsAssignableFrom<IAssuanEventSink>(assembly.GetType("Consumer")!
      .GetMethod("Create")!.Invoke(null, [logger]));
    var info = new AssuanEvent(AssuanComponent.Client, AssuanEventId.RemoteFinal, AssuanOutcome.RemoteError, 3);
    sink.Emit(in info);
    Assert.Null(logger._lastException);
    Assert.Equal((int)AssuanEventId.RemoteFinal, logger._eventId.Id);
    Assert.Contains("RemoteError", logger._message);
    Assert.DoesNotContain("SECRET_MARKER", logger._message);
  }

  private sealed class Scope : IDisposable {
    public void Dispose() { }
  }

  private sealed class RecordingLogger : ILogger {
    internal EventId _eventId;
    internal Exception? _lastException;
    internal string? _message;
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Scope();
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
    Func<TState, Exception?, string> formatter) {
      _message = formatter(state, exception);
      _eventId = eventId;
      _lastException = exception;
    }
  }
}
