// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Runs an explicitly selected gpgconf with a shared time and output budget.</summary>
internal static class GpgconfRunner {
  private const int OUTPUT_LIMIT = 1024 * 1024;
  private const int SCRATCH_BYTES = 8192;

  [SuppressMessage("Design", "MA0051:Method is too long",
    Justification = "Process start, the shared output budget and the cleanup of the process and the secret stdout " +
                    "buffer depend on the same started/transferred flags inside one try/finally.")]
  internal static async Task<SecretBuffer> RunAsync(string executable, string? homeDirectory,
    TransportDeadline deadline, CancellationToken cancellationToken) {
    ArgumentException.ThrowIfNullOrEmpty(executable);
    if (!Path.IsPathFullyQualified(executable)) {
      throw new ArgumentException("The gpgconf executable must be a fully qualified path.", nameof(executable));
    }

    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    if (homeDirectory is not null) {
      start.ArgumentList.Add("--homedir");
      start.ArgumentList.Add(homeDirectory);
    }

    start.ArgumentList.Add("--list-dirs");
    start.ArgumentList.Add("agent-socket");
    using var process = new Process();
    process.StartInfo = start;
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var stdout = new SecretBuffer(OUTPUT_LIMIT);
    var stdoutTask = Task.CompletedTask;
    var stderrTask = Task.CompletedTask;
    var started = false;
    var stdoutTransferred = false;

    try {
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new DiscoveryException(DiscoveryErrorKind.Timeout);
      }

      if (cancellationToken.IsCancellationRequested) {
        throw new DiscoveryException(DiscoveryErrorKind.Cancelled);
      }

      started = process.Start();
      if (!started) {
        throw new DiscoveryException(DiscoveryErrorKind.ProcessFailed);
      }

      process.StandardInput.Close();
      linked.CancelAfter(deadline.Remaining);
      var budget = new OutputBudget();
      stdoutTask = DrainAsync(process.StandardOutput.BaseStream, stdout, budget, linked.Token);
      stderrTask = DrainAsync(process.StandardError.BaseStream, null, budget, linked.Token);
      var exitTask = process.WaitForExitAsync(linked.Token);
      var pending = new List<Task> { stdoutTask, stderrTask, exitTask };
      while (pending.Count != 0) {
        var completed = await Task.WhenAny(pending).WaitAsync(deadline.Remaining, linked.Token).ConfigureAwait(false);
        await completed.ConfigureAwait(false);
        pending.Remove(completed);
      }

      if (deadline.Remaining == TimeSpan.Zero) {
        throw new DiscoveryException(DiscoveryErrorKind.Timeout);
      }

      if (process.ExitCode != 0) {
        throw new DiscoveryException(DiscoveryErrorKind.ProcessFailed);
      }

      stdoutTransferred = true;
      return stdout;
    }
    catch (DiscoveryException) {
      throw;
    }
    catch (OperationCanceledException) {
      throw new DiscoveryException(cancellationToken.IsCancellationRequested
        ? DiscoveryErrorKind.Cancelled
        : DiscoveryErrorKind.Timeout);
    }
    catch (TimeoutException) {
      throw new DiscoveryException(DiscoveryErrorKind.Timeout);
    }
    catch (Exception error) when (error is Win32Exception or IOException) {
      throw new DiscoveryException(DiscoveryErrorKind.Io, error);
    }
    finally {
      await linked.CancelAsync().ConfigureAwait(false);
      if (started) {
        try {
          if (!process.HasExited) {
            process.Kill(true);
          }

          await process.WaitForExitAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(3), cancellationToken)
            .ConfigureAwait(false);
        }
        catch (Exception) {
          /* Process cleanup is the best effort after failure. */
        }
      }

      if (!stdoutTransferred) {
        var drains = Task.WhenAll(stdoutTask, stderrTask);
        if (drains.IsCompleted) {
          _ = drains.Exception;
          stdout.Dispose();
        }
        else {
          _ = drains.ContinueWith(completed =>
          {
            _ = completed.Exception;
            stdout.Dispose();
          }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
      }
    }
  }

  private static async Task DrainAsync(Stream stream, SecretBuffer? collected,
    OutputBudget budget, CancellationToken cancellationToken) {
    var scratch = new byte[SCRATCH_BYTES];
    try {
      while (true) {
        var read = await stream.ReadAsync(scratch.AsMemory(), cancellationToken).ConfigureAwait(false);
        if (read == 0) {
          return;
        }

        budget.Consume(read);
        collected?.Append(scratch.AsSpan(0, read));
      }
    }
    finally {
      CryptographicOperations.ZeroMemory(scratch);
    }
  }

  private sealed class OutputBudget {
    private int _remaining = OUTPUT_LIMIT;

    internal void Consume(int count) {
      int previous;
      do {
        previous = Volatile.Read(ref _remaining);
        if (count > previous) {
          throw new DiscoveryException(DiscoveryErrorKind.OutputLimit);
        }
      } while (Interlocked.CompareExchange(ref _remaining, previous - count, previous) != previous);
    }
  }
}
