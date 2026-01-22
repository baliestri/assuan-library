// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;

namespace AssuanLibrary.Network.Platform.Windows;

/// <summary>
///   Provides functionality to read the port and nonce from GnuPG socket files.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SocketFileReader {
  private const int NONCE_LENGTH = 16;

  /// <summary>
  ///   Reads the port and nonce from the specified GnuPG socket file descriptor.
  /// </summary>
  /// <param name="descriptor">The socket file descriptor to read from.</param>
  /// <returns>A <see cref="PortAndNonce" /> instance containing the port and nonce.</returns>
  /// <remarks>Basically it reads from a file in the machine's filesystem.</remarks>
  public static PortAndNonce Get(SocketDescriptor descriptor) {
    var content = ReadSocketFileContent(descriptor);

    return content.StartsWith("!<socket >"u8)
      ? ParseCygwinStyleSocket(content[10..])
      : ParseClassicGpgSocket(content);
  }

  private static PortAndNonce ParseClassicGpgSocket(ReadOnlySpan<byte> content) {
    if (content.Length < (NONCE_LENGTH + 1)) {
      throw new FormatException("Socket file is too short to contain port + nonce.");
    }

    var splitIndex = content.Length - NONCE_LENGTH;
    var portSpan = content[..splitIndex];
    var nonceSpan = content[splitIndex..];

    return !ushort.TryParse(portSpan, out var port)
      ? throw new FormatException("Invalid port number format in GnuPG socket file.")
      : new PortAndNonce(port, nonceSpan);
  }

  private static PortAndNonce ParseCygwinStyleSocket(ReadOnlySpan<byte> content) {
    if (content.Length < 10) {
      throw new FormatException("Cygwin socket content too short.");
    }

    var spaceIndex = content.IndexOf(Characters.SPACE);
    if (spaceIndex == -1) {
      throw new FormatException("Cannot find port/nonce separator in Cygwin socket file.");
    }

    var portSpan = content[..spaceIndex];

    if (!ushort.TryParse(portSpan, out var port)) {
      throw new FormatException("Invalid port number in Cygwin socket file.");
    }

    var rest = content[spaceIndex..];

    if (!rest.StartsWith(" s "u8)) {
      throw new FormatException("Expected ' s ' prefix before nonce in Cygwin format.");
    }

    var nonce = new byte[NONCE_LENGTH];
    var position = 3;

    for (var i = 0; i < 4; i++) {
      if ((position + 8) > rest.Length) {
        throw new FormatException("Nonce too short in Cygwin format.");
      }

      var hex = Encoding.UTF8.GetString(rest[position..8]);
      if (!uint.TryParse(hex, NumberStyles.HexNumber, null, out var value)) {
        throw new FormatException($"Invalid hex in nonce part {i + 1}.");
      }

      BitConverter.GetBytes(value).CopyTo(nonce, i * 4);

      position += i switch {
        < 3 when rest[position + 8] != (byte)'-' => throw new FormatException("Expected '-' separator in the nonce."),
        3 when rest[position + 8] != (byte)'x' => throw new FormatException("Expected 'x' at the end of nonce."),
        var _ => 9
      };
    }

    return new PortAndNonce(port, nonce);
  }

  private static ReadOnlySpan<byte> ReadSocketFileContent(SocketDescriptor descriptor) {
    var socketPath = GetGpgSocketPath(descriptor);

    if (!File.Exists(socketPath)) {
      throw new FileNotFoundException("GnuPG socket file not found.", socketPath);
    }

    var normalizedPath = socketPath.Replace('\\', '/');

    return File.ReadAllBytes(normalizedPath);
  }

  private static string GetGpgSocketPath(string socketDirKey) {
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
