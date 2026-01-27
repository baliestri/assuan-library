// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.Versioning;
using AssuanLibrary.Endpoints;
using AssuanLibrary.Endpoints.Abstractions;
using AssuanLibrary.Exceptions;
using AssuanLibrary.Platform.Common.Endpoints;

namespace AssuanLibrary.Platform.Windows.Endpoints;

[SupportedOSPlatform("windows")]
internal sealed class TcpClientEndpointResolver : IAssuanEndpointResolver {
  private const int NONCE_LENGTH = 16;

  /// <inheritdoc />
  public IAssuanEndpoint Resolve(AssuanEndpointKind kind) {
    var content = ReadSocketFileContent(kind);

    return content.StartsWith("!<socket >"u8)
      ? ParseCygwinStyleSocket(content[10..])
      : ParseClassicStyleSocket(content);
  }

  private static TcpClientEndpoint ParseClassicStyleSocket(ReadOnlySpan<byte> content) {
    if (content.Length < (NONCE_LENGTH + 1)) {
      throw new FormatException("Socket file is too short to contain port + nonce.");
    }

    var splitIndex = content.Length - NONCE_LENGTH;
    var portSpan = content[..splitIndex];
    var nonceSpan = content[splitIndex..];

    if (!ushort.TryParse(portSpan, out var port)) {
      throw new AssuanEndpointFormatException("Invalid port number format in GnuPG socket file.");
    }

    var endPoint = new IPEndPoint(IPAddress.Loopback, port);
    return new TcpClientEndpoint(endPoint, nonceSpan.ToArray());
  }

  private static TcpClientEndpoint ParseCygwinStyleSocket(ReadOnlySpan<byte> content) {
    if (content.Length < 10) {
      throw new AssuanEndpointFormatException("Cygwin socket content too short.");
    }

    var spaceIndex = content.IndexOf(Characters.SPACE);
    if (spaceIndex == -1) {
      throw new AssuanEndpointFormatException("Cannot find port/nonce separator in Cygwin socket file.");
    }

    var portSpan = content[..spaceIndex];

    if (!ushort.TryParse(portSpan, out var port)) {
      throw new AssuanEndpointFormatException("Invalid port number in Cygwin socket file.");
    }

    var rest = content[spaceIndex..];

    if (!rest.StartsWith(" s "u8)) {
      throw new AssuanEndpointFormatException("Expected ' s ' prefix before nonce in Cygwin format.");
    }

    var nonce = new byte[NONCE_LENGTH];
    var position = 3;

    for (var i = 0; i < 4; i++) {
      if ((position + 8) > rest.Length) {
        throw new AssuanEndpointFormatException("Nonce too short in Cygwin format.");
      }

      var hex = rest[position..8];
      if (!uint.TryParse(hex, NumberStyles.HexNumber, null, out var value)) {
        throw new AssuanEndpointFormatException($"Invalid hex in nonce part {i + 1}.");
      }

      BitConverter.GetBytes(value).CopyTo(nonce, i * 4);

      position += i switch {
        < 3 when rest[position + 8] != (byte)'-' => throw new AssuanEndpointFormatException("Expected '-' separator in the nonce."),
        3 when rest[position + 8] != (byte)'x' => throw new AssuanEndpointFormatException("Expected 'x' at the end of nonce."),
        var _ => 9
      };
    }

    var endPoint = new IPEndPoint(IPAddress.Loopback, port);
    return new TcpClientEndpoint(endPoint, nonce);
  }

  private static ReadOnlySpan<byte> ReadSocketFileContent(AssuanEndpointKind kind) {
    var socketPath = GetSocketPath(kind);

    if (!File.Exists(socketPath)) {
      throw new AssuanEndpointNotFoundException("Socket file not found.");
    }

    var normalizedPath = socketPath.Replace('\\', '/');

    return File.ReadAllBytes(normalizedPath);
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
