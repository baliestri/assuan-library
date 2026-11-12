// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace AssuanLibrary.Tests.Interop;

internal sealed class GnuPgTools {
  private GnuPgTools(string gpgconf, string gpgAgent, string gpgConnectAgent) {
    Gpgconf = gpgconf;
    GpgAgent = gpgAgent;
    GpgConnectAgent = gpgConnectAgent;
  }

  internal string Gpgconf { get; }

  [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global",
    Justification = "Part of the validated single-installation tool set; tests launch the agent through gpgconf.")]
  internal string GpgAgent { get; }

  internal string GpgConnectAgent { get; }

  internal static GnuPgTools FromEnvironment() {
    if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.Ordinal)) {
      throw new InvalidOperationException("Real GnuPG interoperability requires an isolated hosted runner.");
    }

    return Require(Environment.GetEnvironmentVariable("ASSUAN_GPGCONF_PATH"),
      Environment.GetEnvironmentVariable("ASSUAN_GPG_AGENT_PATH"),
      Environment.GetEnvironmentVariable("ASSUAN_GPG_CONNECT_AGENT_PATH"));
  }

  internal static GnuPgTools Require(string? gpgconf, string? gpgAgent, string? gpgConnectAgent) {
    var paths = new[] { gpgconf, gpgAgent, gpgConnectAgent };
    if (paths.Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))) {
      throw new InvalidOperationException("Required GnuPG executables are unavailable.");
    }

    var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    if (paths.Select(Path.GetDirectoryName).Distinct(comparer).Count() == 1) {
      return new GnuPgTools(gpgconf!, gpgAgent!, gpgConnectAgent!);
    }

    throw new InvalidOperationException("GnuPG executables must come from one installation.");
  }
}
