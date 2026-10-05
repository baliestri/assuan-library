// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Bounded native Windows agent socket-file parsing; never prints nonce bytes.</summary>
internal static class WindowsSocketFile {
  internal static async Task<(ushort Port, SecretBuffer Nonce)> ReadAsync(string path,
  TransportDeadline deadline, CancellationToken cancellationToken) {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    try {
      var attributes = File.GetAttributes(path);
      if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0) {
        throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
      }

      await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
        1, FileOptions.Asynchronous);
      return await ReadStreamAsync(file, deadline, cancellationToken).ConfigureAwait(false);
    }
    catch (DiscoveryException) { throw; }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
      throw new DiscoveryException(DiscoveryErrorKind.Io, error);
    }
  }

  // Kept separate so delayed reads can be tested without depending on OS file-I/O cancellation.
  internal static async Task<(ushort Port, SecretBuffer Nonce)> ReadStreamAsync(Stream file,
  TransportDeadline deadline, CancellationToken cancellationToken) {
    var scratch = new byte[23];
    var deferred = false;
    try {
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      if (deadline.Remaining == TimeSpan.Zero) {
        throw new DiscoveryException(DiscoveryErrorKind.Timeout);
      }

      linked.CancelAfter(deadline.Remaining);
      var used = 0;
      while (used < scratch.Length) {
        var remaining = deadline.Remaining;
        if (remaining == TimeSpan.Zero) {
          throw new DiscoveryException(DiscoveryErrorKind.Timeout);
        }

        var pendingRead = file.ReadAsync(scratch.AsMemory(used), linked.Token).AsTask();
        int read;
        try { read = await pendingRead.WaitAsync(remaining, linked.Token).ConfigureAwait(false); }
        catch {
          if (!pendingRead.IsCompleted) {
            deferred = true;
            _ = pendingRead.ContinueWith(completed => {
              _ = completed.Exception;
              CryptographicOperations.ZeroMemory(scratch);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
          }

          throw;
        }

        if (read == 0) {
          break;
        }

        used += read;
      }

      if (deadline.Remaining == TimeSpan.Zero) {
        throw new DiscoveryException(DiscoveryErrorKind.Timeout);
      }

      return Parse(scratch.AsSpan(0, used));
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
    catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
      throw new DiscoveryException(DiscoveryErrorKind.Io, error);
    }
    finally {
      if (!deferred) {
        CryptographicOperations.ZeroMemory(scratch);
      }
    }
  }

  internal static (ushort Port, SecretBuffer Nonce) Parse(ReadOnlySpan<byte> bytes) {
    if (bytes.StartsWith("!<socket >"u8)) {
      throw new DiscoveryException(DiscoveryErrorKind.UnsupportedFormat);
    }

    if (bytes.Length is < 18 or > 22) {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    var separator = bytes.IndexOf((byte)'\n');
    if (separator is < 1 or > 5 ||
        bytes.Length - separator - 1 != 16) {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    uint port = 0;
    foreach (var digit in bytes[..separator]) {
      if (digit is < (byte)'0' or > (byte)'9') {
        throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
      }

      port = (port * 10) + (uint)(digit - (byte)'0');
      if (port > ushort.MaxValue) {
        throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
      }
    }

    if (port == 0) {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    var nonce = new SecretBuffer(16);
    nonce.Append(bytes[(separator + 1)..]);

    return ((ushort)port, nonce);
  }
}
