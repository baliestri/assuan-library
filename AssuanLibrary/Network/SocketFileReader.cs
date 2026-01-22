// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace AssuanLibrary.Network;

/// <summary>
///   Provides functionality to read the port and nonce from GnuPG socket files.
/// </summary>
public static partial class SocketFileReader {
  /// <summary>
  ///   Gets the socket path for the specified socket directory key using <c>gpgconf</c>.
  /// </summary>
  /// <param name="socketDirKey">The socket directory key to query (e.g., "socketdir").</param>
  /// <returns>The socket path as a string.</returns>
  /// <exception cref="InvalidOperationException">Thrown if the command fails or returns no output.</exception>
  public static string GetSocketPath(string socketDirKey) {
    var psi = new ProcessStartInfo {
      FileName = "gpgconf",
      Arguments = $"--list-dirs {socketDirKey}",
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using var process = Process.Start(psi) ??
                        throw new InvalidOperationException("Failed to start gpgconf process.");

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (process.ExitCode != 0) {
      throw new InvalidOperationException($"gpgconf --list-dirs failed (exit code {process.ExitCode}): {error}");
    }

    var lines = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

    return lines.Length == 0
      ? throw new InvalidOperationException($"No output from gpgconf --list-dirs {socketDirKey}")
      : lines[0].Trim();
  }
}
