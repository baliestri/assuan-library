// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Parses the literal, unescaped pathname returned by gpgconf --list-dirs agent-socket.</summary>
internal static class AgentOutputParser {
  private static readonly UTF8Encoding _strictUtf8 = new(false, true);

  internal static byte[] ParseUnix(ReadOnlySpan<byte> output) {
    var path = WithoutTerminator(output, false);
    if (path[0] != (byte)'/') {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    return path.ToArray();
  }

  internal static string ParseWindows(ReadOnlySpan<byte> output) {
    var path = WithoutTerminator(output, true);
    string value;

    try { value = _strictUtf8.GetString(path); }
    catch (DecoderFallbackException) { throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput); }

    // Validate the Windows form even when deterministic parser tests run on Unix.
    var drive = value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' &&
                value[2] is '\\' or '/';
    var share = value.AsSpan(value.StartsWith(@"\\", StringComparison.Ordinal) ? 2 : value.Length);
    var slash = share.IndexOfAny('\\', '/');
    var unc = value.StartsWith(@"\\", StringComparison.Ordinal) && slash > 0 && slash < (share.Length - 1);
    if ((!drive && !unc) ||
        (OperatingSystem.IsWindows() && !Path.IsPathFullyQualified(value))) {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    return value;
  }

  private static ReadOnlySpan<byte> WithoutTerminator(ReadOnlySpan<byte> output, bool windows) {
    if (output.Length < 2 ||
        output[^1] != (byte)'\n') {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    var path = output[..^1];
    if (windows &&
        !path.IsEmpty &&
        path[^1] == (byte)'\r') {
      path = path[..^1];
    }

    if (path.IsEmpty ||
        path.IndexOfAny((byte)0, (byte)'\n') >= 0) {
      throw new DiscoveryException(DiscoveryErrorKind.InvalidOutput);
    }

    return path;
  }
}
