// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssuanLibrary.Extensions.DependencyInjection;

internal static class AssuanLoggingOptionsExtensions {
  public static void UseMicrosoftLoggerWhenAvailable(this AssuanLoggingOptions options, IServiceProvider serviceProvider, string categoryName) {
    if (!ReferenceEquals(options.Logger, NullAssuanLogger.Instance)) {
      return;
    }

    var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
    if (loggerFactory is null) {
      return;
    }

    options.Logger = new MicrosoftAssuanLogger(loggerFactory.CreateLogger(categoryName));
  }
}
