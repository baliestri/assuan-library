// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Tests.Transport;

internal sealed class UnixFixture : IDisposable {
  internal string Root { get; }

  internal UnixFixture() {
    if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
    var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    Root = Path.Combine(repository, "artifacts", "ipc-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Root);
    File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
  }

  internal string Socket(string name = "socket") => Path.Combine(Root, name);

  public void Dispose() {
    if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
  }
}
