// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.InteropServices;

namespace AssuanLibrary.Transport.Unix;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnixFileIdentity(ulong Device, ulong Inode, uint Uid, uint Mode) {
  private bool IsDirectory => (Mode & 0xf000) == 0x4000;
  internal bool IsSocket => (Mode & 0xf000) == 0xc000;
  private bool IsSymlink => (Mode & 0xf000) == 0xa000;

  internal bool IsPrivateDirectory(uint uid)
    => IsDirectory && !IsSymlink && Uid == uid && (Mode & 0x3f) == 0;

  internal bool SameFile(UnixFileIdentity other)
    => Device == other.Device && Inode == other.Inode;
}
