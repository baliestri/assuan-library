// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers.Binary;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace AssuanLibrary.Transport.Unix;

internal readonly record struct UnixFileIdentity(ulong Device, ulong Inode, uint Uid, uint Mode) {
  internal bool IsDirectory => (Mode & 0xf000) == 0x4000;
  internal bool IsSocket => (Mode & 0xf000) == 0xc000;
  internal bool IsSymlink => (Mode & 0xf000) == 0xa000;

  internal bool IsPrivateDirectory(uint uid)
    => IsDirectory && !IsSymlink && Uid == uid && (Mode & 0x3f) == 0;

  internal bool SameFile(UnixFileIdentity other)
    => Device == other.Device && Inode == other.Inode;
}

internal readonly record struct UnixCredentials(uint Uid, uint Gid, uint? Pid);

/// <summary>Small, OS-specific native boundary for metadata and Unix peer credentials.</summary>
internal static partial class UnixNative {
  private const int STAT_BUFFER_BYTES = 256;
  private const int NOT_FOUND = 2;
  private const int SOL_SOCKET_LINUX = 1;
  private const int SO_PEER_CRED_LINUX = 17;
  private const int TRY_AGAIN_LINUX = 11;
  private const int TRY_AGAIN_MAC = 35;
  private const int INTERRUPTED = 4;
  private const int BAD_DESCRIPTOR = 9;
  private const int LINUX_NONBLOCKING_CLOSE_ON_EXEC = 0x80800;

  internal static uint EffectiveUid {
    get {
      EnsureSupported();
      try { return OperatingSystem.IsLinux() ? LinuxEffectiveUid() : MacEffectiveUid(); }
      catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) {
        throw new TransportException(TransportErrorKind.AccessPolicy, error);
      }
    }
  }

  [LibraryImport("libc", EntryPoint = "geteuid")]
  private static partial uint LinuxEffectiveUid();

  [LibraryImport("libSystem.B.dylib", EntryPoint = "geteuid")]
  private static partial uint MacEffectiveUid();

  [LibraryImport("libc", EntryPoint = "lstat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
  private static partial int LinuxLStat(string path, [Out] byte[] result);

  [LibraryImport("libSystem.B.dylib", EntryPoint = "lstat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
  private static partial int MacLStat(string path, [Out] byte[] result);

  [LibraryImport("libc", EntryPoint = "realpath", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
  private static partial IntPtr LinuxRealPath(string path, IntPtr resolved);

  [LibraryImport("libSystem.B.dylib", EntryPoint = "realpath", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
  private static partial IntPtr MacRealPath(string path, IntPtr resolved);

  [LibraryImport("libc", EntryPoint = "free")]
  private static partial void LinuxFree(IntPtr memory);

  [LibraryImport("libSystem.B.dylib", EntryPoint = "free")]
  private static partial void MacFree(IntPtr memory);

  [LibraryImport("libc", EntryPoint = "getsockopt", SetLastError = true)]
  private static partial int LinuxGetSockOpt(int fd, int level, int option, ref LinuxPeerCred value, ref uint size);

  [LibraryImport("libSystem.B.dylib", EntryPoint = "getpeereid", SetLastError = true)]
  private static partial int MacGetPeerEid(int fd, out uint uid, out uint gid);

  [LibraryImport("libc", EntryPoint = "bind", SetLastError = true)]
  private static partial int LinuxBind(int fd, [In] byte[] address, uint addressLength);

  [LibraryImport("libSystem.B.dylib", EntryPoint = "bind", SetLastError = true)]
  private static partial int MacBind(int fd, [In] byte[] address, uint addressLength);

  [LibraryImport("libc", EntryPoint = "accept4", SetLastError = true)]
  private static partial int LinuxAccept(int fd, IntPtr address, IntPtr addressLength, int flags);

  [LibraryImport("libSystem.B.dylib", EntryPoint = "accept", SetLastError = true)]
  private static partial int MacAccept(int fd, IntPtr address, IntPtr addressLength);

  [LibraryImport("libSystem.B.dylib", EntryPoint = "fcntl", SetLastError = true)]
  private static partial int MacFcntl(int fd, int command, int value);

  internal static UnixFileIdentity InspectNoFollow(string path) {
    if (!TryInspectNoFollow(path, out var identity)) {
      throw new TransportException(TransportErrorKind.Io,
        new FileNotFoundException("Socket path does not exist."));
    }

    return identity;
  }

  internal static bool TryInspectNoFollow(string path, out UnixFileIdentity identity) {
    EnsureSupported();
    var buffer = new byte[STAT_BUFFER_BYTES];
    int result;
    try { result = OperatingSystem.IsLinux() ? LinuxLStat(path, buffer) : MacLStat(path, buffer); }
    catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }

    if (result != 0) {
      var errno = Marshal.GetLastPInvokeError();
      identity = default;

      return errno == NOT_FOUND ? false : throw new TransportException(TransportErrorKind.Io, new Win32Exception(errno));
    }

    identity = ParseMetadata(buffer);
    return true;
  }

  internal static string Canonicalize(string path) {
    EnsureSupported();
    IntPtr resolved;
    try { resolved = OperatingSystem.IsLinux() ? LinuxRealPath(path, IntPtr.Zero) : MacRealPath(path, IntPtr.Zero); }
    catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }

    if (resolved == IntPtr.Zero) {
      throw new TransportException(TransportErrorKind.Io,
        new Win32Exception(Marshal.GetLastPInvokeError()));
    }

    try {
      return Marshal.PtrToStringUTF8(resolved) ?? throw new TransportException(TransportErrorKind.AccessPolicy);
    }
    finally {
      if (OperatingSystem.IsLinux()) {
        LinuxFree(resolved);
      }
      else {
        MacFree(resolved);
      }
    }
  }

  internal static UnixCredentials PeerCredentials(Socket socket) {
    EnsureSupported();
    var handle = socket.SafeHandle;
    var added = false;
    try {
      handle.DangerousAddRef(ref added);
      var fd = checked((int)handle.DangerousGetHandle().ToInt64());
      if (OperatingSystem.IsLinux()) {
        var credentials = new LinuxPeerCred();
        var size = (uint)Marshal.SizeOf<LinuxPeerCred>();
        if (LinuxGetSockOpt(fd, SOL_SOCKET_LINUX, SO_PEER_CRED_LINUX, ref credentials, ref size) != 0 ||
            size != Marshal.SizeOf<LinuxPeerCred>()) {
          throw new TransportException(TransportErrorKind.AccessPolicy);
        }

        return new UnixCredentials(credentials.Uid, credentials.Gid,
          credentials.Pid < 0 ? null : (uint)credentials.Pid);
      }

      if (MacGetPeerEid(fd, out var uid, out var gid) != 0) {
        throw new TransportException(TransportErrorKind.AccessPolicy);
      }

      return new UnixCredentials(uid, gid, null);
    }
    catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }
    finally {
      if (added) {
        handle.DangerousRelease();
      }
    }
  }

  internal static void AuthorizeUid(uint actual, uint expected) {
    if (actual != expected) {
      throw new TransportException(TransportErrorKind.AccessPolicy);
    }
  }

  internal static void BindSocket(Socket socket, string path) {
    EnsureSupported();
    byte[] encoded;
    try { encoded = new UTF8Encoding(false, true).GetBytes(path); }
    catch (EncoderFallbackException) { throw new TransportException(TransportErrorKind.InvalidEndpoint); }

    // Linux sockaddr_un: 2-byte family + 108-byte path; macOS: len + family + 104-byte path.
    var maximum = OperatingSystem.IsLinux() ? 107 : 103; // Reserve one NUL terminator.
    if (encoded.Length == 0 ||
        encoded.Length > maximum) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    var length = checked((uint)(2 + encoded.Length + 1));
    var address = new byte[(int)length];
    if (OperatingSystem.IsLinux()) {
      address[0] = 1;
    }
    else {
      address[0] = (byte)length;
      address[1] = 1;
    }

    encoded.AsSpan().CopyTo(address.AsSpan(2));
    var handle = socket.SafeHandle;
    var added = false;
    try {
      handle.DangerousAddRef(ref added);
      var fd = checked((int)handle.DangerousGetHandle().ToInt64());
      var status = OperatingSystem.IsLinux() ? LinuxBind(fd, address, length) : MacBind(fd, address, length);
      if (status != 0) {
        throw new TransportException(TransportErrorKind.Io,
          new Win32Exception(Marshal.GetLastPInvokeError()));
      }
    }
    catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }
    finally {
      if (added) {
        handle.DangerousRelease();
      }
    }
  }

  internal static Socket? TryAccept(Socket listener) {
    EnsureSupported();
    var handle = listener.SafeHandle;
    var added = false;
    int result;
    try {
      handle.DangerousAddRef(ref added);
      var fd = checked((int)handle.DangerousGetHandle().ToInt64());
      result = OperatingSystem.IsLinux()
        ? LinuxAccept(fd, IntPtr.Zero, IntPtr.Zero, LINUX_NONBLOCKING_CLOSE_ON_EXEC)
        : MacAccept(fd, IntPtr.Zero, IntPtr.Zero);
    }
    catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) {
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }
    finally {
      if (added) {
        handle.DangerousRelease();
      }
    }

    if (result < 0) {
      var errno = Marshal.GetLastPInvokeError();
      if (errno is INTERRUPTED ||
          errno == (OperatingSystem.IsLinux() ? TRY_AGAIN_LINUX : TRY_AGAIN_MAC)) {
        return null;
      }

      if (errno == BAD_DESCRIPTOR) {
        throw new TransportException(TransportErrorKind.Closed);
      }

      throw new TransportException(TransportErrorKind.Io, new Win32Exception(errno));
    }

    var acceptedHandle = new SafeSocketHandle(result, true);
    try {
      if (OperatingSystem.IsMacOS() &&
          MacFcntl(result, 2, 1) != 0) {
        throw new TransportException(TransportErrorKind.AccessPolicy);
      }

      return new Socket(acceptedHandle);
    }
    catch {
      acceptedHandle.Dispose();
      throw;
    }
  }

  private static UnixFileIdentity ParseMetadata(ReadOnlySpan<byte> bytes) {
    ulong dev;
    ulong ino;
    uint uid;
    uint mode;
    if (OperatingSystem.IsMacOS()) {
      dev = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
      mode = BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]);
      ino = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
      uid = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
    }
    else {
      dev = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
      ino = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
      var modeOffset = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 24 : 16;
      var uidOffset = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 28 : 24;
      mode = BinaryPrimitives.ReadUInt32LittleEndian(bytes[modeOffset..]);
      uid = BinaryPrimitives.ReadUInt32LittleEndian(bytes[uidOffset..]);
    }

    return new UnixFileIdentity(dev, ino, uid, mode);
  }

  private static void EnsureSupported() {
    if ((!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) ||
        !BitConverter.IsLittleEndian ||
        RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64)) {
      throw new TransportException(TransportErrorKind.AccessPolicy);
    }
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct LinuxPeerCred {
    internal int Pid;
    internal uint Uid;
    internal uint Gid;
  }
}
