// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests.Transport;

internal sealed class UnixFixture : IDisposable {
  internal UnixFixture() {
    if (OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    // Keep socket paths below macOS's 103-byte native sockaddr_un limit even
    // when the hosted workspace prefix is long.
    Root = Path.Combine(repository, "artifacts", "i", Guid.NewGuid().ToString("N")[..12]);
    Directory.CreateDirectory(Root);
    File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
  }

  internal string Root { get; }

  public void Dispose() {
    if (Directory.Exists(Root)) {
      Directory.Delete(Root, true);
    }
  }

  internal string Socket(string name = "socket")
    => Path.Combine(Root, name);
}
