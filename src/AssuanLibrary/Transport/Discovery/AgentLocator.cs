// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport.Discovery;

/// <summary>Discovers an already existing GnuPG agent using an explicitly selected gpgconf executable.</summary>
public sealed class AgentLocator {
  private readonly string _executable;
  private readonly string? _homeDirectory;

  /// <summary>Stores explicit paths without starting a subprocess or agent.</summary>
  public AgentLocator(string executablePath, string? homeDirectory = null) {
    ArgumentException.ThrowIfNullOrEmpty(executablePath);
    _executable = executablePath;
    _homeDirectory = homeDirectory;
  }

  /// <summary>Resolves a Unix socket or a native Windows loopback port and protected nonce.</summary>
  /// <remarks>One five-second budget covers gpgconf and any native Windows file read.</remarks>
  public async Task<ResolvedAgentEndpoint> ResolveAsync(CancellationToken cancellationToken = default) {
    if (!OperatingSystem.IsWindows() &&
        !OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      throw new PlatformNotSupportedException();
    }

    var deadline = new TransportDeadline(TimeSpan.FromSeconds(5));
    using var output = await GpgconfRunner.RunAsync(_executable, _homeDirectory, deadline, cancellationToken)
      .ConfigureAwait(false);
    if (!OperatingSystem.IsWindows()) {
      return ResolvedAgentEndpoint.ForUnix(AgentOutputParser.ParseUnix(output.Expose()));
    }

    var path = AgentOutputParser.ParseWindows(output.Expose());
    var (port, nonce) = await WindowsSocketFile.ReadAsync(path, deadline, cancellationToken).ConfigureAwait(false);

    return ResolvedAgentEndpoint.ForWindows(port, nonce);
  }

  /// <inheritdoc />
  public override string ToString()
    => $"AgentLocator {{ HasHomeDirectory = {_homeDirectory is not null} }}";
}
