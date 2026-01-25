// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;

namespace AssuanLibrary.Platform.Unix.Polyfills;

/// <summary>
///   Polyfill extension methods for <see cref="Socket" />.
/// </summary>
internal static class SocketPolyfillExtensions {
  extension(Socket socket) {
    /// <summary>
    ///   Asynchronously receives data from a connected <see cref="Socket" />.
    /// </summary>
    /// <param name="arraySegment">The buffer to store the received data.</param>
    /// <param name="socketFlags">The socket flags to use when receiving data.</param>
    /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous receive operation. The result contains the number of bytes received.</returns>
    public Task<int> ReceiveAsync(ArraySegment<byte> arraySegment, SocketFlags socketFlags, CancellationToken ct = default) {
      ArgumentNullException.ThrowIfNull(socket);

      if (ct.IsCancellationRequested) {
        return Task.FromCanceled<int>(ct);
      }

      var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
      var args = new SocketAsyncEventArgs {
        SocketFlags = socketFlags
      };
      args.SetBuffer(arraySegment.Array, arraySegment.Offset, arraySegment.Count);

      args.Completed += CompletedHandler;

      var ctr = default(CancellationTokenRegistration);

      if (ct.CanBeCanceled) {
        ctr = ct.Register(() => {
          try {
            socket.Dispose();
          }
          catch {
            // ignored
          }

          tcs.TrySetCanceled(ct);
        });
      }

      try {
        if (!socket.ReceiveAsync(args)) {
          CompletedHandler(socket, args);
        }
      }
      catch (Exception ex) {
        CleanupHandler();
        tcs.TrySetException(ex);
      }

      ctr.Dispose();
      return tcs.Task;

      void CompletedHandler(object? sender, SocketAsyncEventArgs eventArgs) {
        CleanupHandler();

        if (eventArgs.SocketError == SocketError.Success) {
          tcs.TrySetResult(eventArgs.BytesTransferred);
          return;
        }

        tcs.TrySetException(new SocketException((int)eventArgs.SocketError));
      }

      void CleanupHandler() {
        args.Completed -= CompletedHandler;
        args.Dispose();
      }
    }

    /// <summary>
    ///   Asynchronously sends data to a connected <see cref="Socket" />.
    /// </summary>
    /// <param name="arraySegment">The buffer containing the data to send.</param>
    /// <param name="socketFlags">The socket flags to use when sending data.</param>
    /// <param name="ct">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous send operation. The result contains the number of bytes sent.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="arraySegment.Array" /> is <c>null</c>.</exception>
    public Task<int> SendAsync(ArraySegment<byte> arraySegment, SocketFlags socketFlags, CancellationToken ct = default) {
      ArgumentNullException.ThrowIfNull(socket);

      if (arraySegment.Array is null) {
        throw new ArgumentNullException(nameof(arraySegment.Array));
      }

      if (ct.IsCancellationRequested) {
        return Task.FromCanceled<int>(ct);
      }

      var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
      var args = new SocketAsyncEventArgs {
        SocketFlags = socketFlags
      };
      args.SetBuffer(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
      args.Completed += CompletedHandler;

      var ctr = default(CancellationTokenRegistration);
      if (ct.CanBeCanceled) {
        ctr = ct.Register(() => {
          try {
            socket.Dispose();
          }
          catch {
            // ignored
          }

          tcs.TrySetCanceled(ct);
        });
      }

      try {
        if (!socket.SendAsync(args)) {
          CompletedHandler(socket, args);
        }
      }
      catch (Exception ex) {
        CleanupHandler();
        tcs.TrySetException(ex);
      }

      ctr.Dispose();
      return tcs.Task;

      void CompletedHandler(object? sender, SocketAsyncEventArgs eventArgs) {
        CleanupHandler();

        if (eventArgs.SocketError == SocketError.Success) {
          tcs.TrySetResult(eventArgs.BytesTransferred);
          return;
        }

        tcs.TrySetException(new SocketException((int)eventArgs.SocketError));
      }

      void CleanupHandler() {
        args.Completed -= CompletedHandler;
        args.Dispose();
      }
    }
  }
}
