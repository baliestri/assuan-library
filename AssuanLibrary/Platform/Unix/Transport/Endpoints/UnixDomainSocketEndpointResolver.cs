// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Runtime.Versioning;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Transport.Endpoints;

namespace AssuanLibrary.Platform.Unix.Transport.Endpoints;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class UnixDomainSocketEndpointResolver : IAssuanEndpointResolver {
  /// <inheritdoc />
  public IAssuanEndpoint Resolve(AssuanEndpointKind kind) {
    var socketPath = GetSocketPath(kind);

    if (!File.Exists(socketPath)) {
      throw new AssuanEndpointNotFoundException("Socket file not found.");
    }

    var normalizedPath = socketPath.Replace('\\', '/');

    return new UnixDomainSocketEndpoint(normalizedPath);
  }

  private static string GetSocketPath(string kindKey) {
    var psi = new ProcessStartInfo {
      FileName = "gpgconf",
      Arguments = $"--list-dirs {kindKey}",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = Process.Start(psi) ??
                        throw new AssuanEndpointResolverException("Could not start 'gpgconf' process.");

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (process.ExitCode != 0) {
      throw new AssuanEndpointResolverException("'gpgconf' process exited with a non-zero exit code.",
        new Exception($"Exit Code: {process.ExitCode}. Error: {error.Trim()}"));
    }

    var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

    return lines.Length == 0
      ? throw new AssuanEndpointResolverException("'gpgconf' did not return any output.")
      : lines[0].Trim();
  }
}
