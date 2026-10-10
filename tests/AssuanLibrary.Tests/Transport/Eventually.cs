// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests.Transport;

internal static class Eventually {
  internal static async Task<bool> UntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken token) {
    using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
    wait.CancelAfter(timeout);
    while (!condition()) {
      try { await Task.Delay(10, wait.Token); }
      catch (OperationCanceledException) when (!token.IsCancellationRequested) { return condition(); }
    }

    return true;
  }
}
