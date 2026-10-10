// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary.Tests.Transport;

internal sealed class AgentFixture : IDisposable {
  private readonly string _repository;

  internal AgentFixture(string mode = "success", byte[]? output = null) {
    _repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    // Keep the deliberate space/percent sequence while fitting a Unix socket
    // below macOS's 103-byte native path budget in hosted workspaces.
    Home = Path.Combine(_repository, "artifacts", "a", "h %20 " + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(Home);
    File.WriteAllText(Path.Combine(Home, "mode.txt"), mode);
    File.WriteAllBytes(Path.Combine(Home, "output.bin"), output ?? Encoding.UTF8.GetBytes(SocketFile + "\n"));
  }

  internal string Home { get; }

  internal string Executable {
    get {
      var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
      var name = "AssuanLibrary.GpgconfFixture" + (OperatingSystem.IsWindows() ? ".exe" : "");
      return Path.Combine(_repository, "tests", "AssuanLibrary.GpgconfFixture", "bin", configuration, "net10.0", name);
    }
  }

  internal string SocketFile => Path.Combine(Home, "socket %20 literal");

  public void Dispose() {
    if (Directory.Exists(Home)) {
      Directory.Delete(Home, true);
    }
  }
}
