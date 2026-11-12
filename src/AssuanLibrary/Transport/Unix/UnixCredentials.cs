// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.InteropServices;

namespace AssuanLibrary.Transport.Unix;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnixCredentials(uint Uid, uint Gid, uint? Pid);
