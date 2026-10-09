// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Transport;

/// <summary>Kind of explicit local byte-stream endpoint.</summary>
public enum LocalEndpointKind {
  /// <summary>A filesystem Unix-domain socket.</summary>
  UnixSocket,

  /// <summary>A local Windows named pipe.</summary>
  NamedPipe
}

/// <summary>A caller-supplied Unix socket or local Windows named pipe path.</summary>
public sealed class LocalEndpoint {
  private const string PIPE_PREFIX = @"\\.\pipe\";

  private LocalEndpoint(LocalEndpointKind kind, string path) {
    Kind = kind;
    Path = path;
  }

  /// <summary>The local backend to use; no custom backend needs an endpoint variant.</summary>
  public LocalEndpointKind Kind { get; }

  /// <summary>The explicit path; using this property is an intentional disclosure by the caller.</summary>
  public string Path { get; }

  /// <summary>Creates an endpoint for an existing or new filesystem Unix socket.</summary>
  /// <remarks>Listener policy is enforced when binding, not when connecting to another service.</remarks>
  public static LocalEndpoint Unix(string path) {
    if (!OperatingSystem.IsLinux() &&
        !OperatingSystem.IsMacOS()) {
      throw new PlatformNotSupportedException();
    }

    ArgumentNullException.ThrowIfNull(path);

    if (path.Length == 0 ||
        path.Contains('\0')) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    return new LocalEndpoint(LocalEndpointKind.UnixSocket, path);
  }

  /// <summary>Creates an endpoint for a local Windows byte-mode named pipe.</summary>
  public static LocalEndpoint NamedPipe(string path) {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException();
    }

    ArgumentNullException.ThrowIfNull(path);

    if (!path.StartsWith(PIPE_PREFIX, StringComparison.Ordinal) ||
        path.Length <= PIPE_PREFIX.Length ||
        path.Length > 256 ||
        path.AsSpan(PIPE_PREFIX.Length).IndexOfAny('\\', '/', '\0') >= 0) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    return new LocalEndpoint(LocalEndpointKind.NamedPipe, path);
  }

  /// <inheritdoc />
  public override string ToString()
    => $"LocalEndpoint {{ Kind = {Kind} }}";
}
