// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AssuanLibrary.Transport.Windows;

internal static partial class PipeNative {
  private const uint PIPE_ACCESS_DUPLEX = 0x00000003;
  private const uint FIRST_PIPE_INSTANCE = 0x00080000;
  private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
  private const uint REJECT_REMOTE_CLIENTS = 0x00000008;
  private const uint UNLIMITED_INSTANCES = 255;
  private const uint GENERIC_READ_WRITE = 0xC0000000;
  private const uint OPEN_EXISTING = 3;
  private const int ERROR_PIPE_BUSY = 231;
  private const int ERROR_SEMAPHORE_TIMEOUT = 121;

  [LibraryImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
  private static partial SafePipeHandle CreateNamedPipe(string name, uint openMode, uint pipeMode,
  uint instances, uint outBufferSize, uint inBufferSize, uint timeout, ref SecurityAttributes security);

  [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
  private static partial SafePipeHandle CreateFile(string name, uint desiredAccess, uint shareMode,
  IntPtr security, uint creation, uint flags, IntPtr template);

  internal static NamedPipeServerStream CreateWaiting(string path, byte[] securityDescriptor, bool firstInstance) {
    var descriptor = Marshal.AllocHGlobal(securityDescriptor.Length);
    try {
      Marshal.Copy(securityDescriptor, 0, descriptor, securityDescriptor.Length);
      var attributes = new SecurityAttributes {
        Length = Marshal.SizeOf<SecurityAttributes>(),
        Descriptor = descriptor,
        InheritHandle = 0
      };
      var openMode = PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | (firstInstance ? FIRST_PIPE_INSTANCE : 0);
      var handle = CreateNamedPipe(path, openMode, REJECT_REMOTE_CLIENTS, UNLIMITED_INSTANCES, 4096, 4096, 0, ref attributes);
      if (handle.IsInvalid) {
        var error = Marshal.GetLastPInvokeError();
        handle.Dispose();
        throw new TransportException(TransportErrorKind.Io, new Win32Exception(error));
      }

      try {
        return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle);
      }
      catch {
        handle.Dispose();
        throw;
      }
    }
    finally {
      Marshal.FreeHGlobal(descriptor);
    }
  }

  // A zero-wait CreateFile attempts one connect immediately. Busy pipes are retried
  // asynchronously by PipeConnector, never through a blocking connection worker.
  internal static NamedPipeClientStream? TryConnect(string path) {
    var handle = CreateFile(path, GENERIC_READ_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
    if (handle.IsInvalid) {
      var error = Marshal.GetLastPInvokeError();
      handle.Dispose();
      if (error is ERROR_PIPE_BUSY or ERROR_SEMAPHORE_TIMEOUT) {
        return null;
      }

      throw new TransportException(TransportErrorKind.Io, new Win32Exception(error));
    }

    try {
      return new NamedPipeClientStream(PipeDirection.InOut, true, true, handle);
    }
    catch {
      handle.Dispose();
      throw;
    }
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct SecurityAttributes {
    internal int Length;
    internal IntPtr Descriptor;
    internal int InheritHandle;
  }
}
