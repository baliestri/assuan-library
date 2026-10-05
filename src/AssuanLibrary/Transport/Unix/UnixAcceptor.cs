// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;

namespace AssuanLibrary.Transport.Unix;

/// <summary>User-owned Unix socket listener with verified peer UID and identity-safe explicit cleanup.</summary>
public sealed class UnixAcceptor : IAssuanAcceptor {
  private readonly UnixFileIdentity _parentIdentity;
  private readonly UnixFileIdentity _socketIdentity;
  private readonly uint _uid;
  private bool _cleaned;
  private Socket? _listener;

  private UnixAcceptor(Socket listener, string path, uint uid, UnixFileIdentity parent,
  UnixFileIdentity socket) {
    _listener = listener;
    Endpoint = LocalEndpoint.Unix(path);
    _uid = uid;
    _parentIdentity = parent;
    _socketIdentity = socket;
  }

  /// <summary>The actual bound absolute filesystem socket path.</summary>
  public LocalEndpoint Endpoint { get; }

  /// <summary>Accepts a connection only after the OS reports the current effective user's UID.</summary>
  public async ValueTask<AcceptedConnection> AcceptAsync(CancellationToken cancellationToken = default) {
    Socket accepted;
    try {
      while (true) {
        cancellationToken.ThrowIfCancellationRequested();
        var listener = _listener ?? throw new TransportException(TransportErrorKind.Closed);
        var incoming = UnixNative.TryAccept(listener);
        if (incoming is not null) {
          if (cancellationToken.IsCancellationRequested) {
            incoming.Dispose();
            throw new OperationCanceledException(cancellationToken);
          }

          accepted = incoming;
          break;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
      }
    }
    catch (OperationCanceledException) {
      throw new TransportException(TransportErrorKind.Cancelled);
    }
    catch (ObjectDisposedException) {
      throw new TransportException(TransportErrorKind.Closed);
    }
    catch (SocketException error) {
      throw new TransportException(_listener is null ? TransportErrorKind.Closed : TransportErrorKind.Io, error);
    }

    try {
      var credentials = UnixNative.PeerCredentials(accepted);
      UnixNative.AuthorizeUid(credentials.Uid, _uid);
      if (_listener is null) {
        throw new TransportException(TransportErrorKind.Closed);
      }

      return new AcceptedConnection(new NetworkStream(accepted, true),
        new UnixPeerIdentity(credentials.Uid, credentials.Gid, credentials.Pid));
    }
    catch {
      accepted.Dispose();
      throw;
    }
  }

  /// <summary>Closes the listener without unlinking the socket pathname.</summary>
  public ValueTask DisposeAsync() {
    Interlocked.Exchange(ref _listener, null)?.Dispose();
    return ValueTask.CompletedTask;
  }

  /// <summary>Creates a restricted mode 0600 socket in an existing private current-user directory.</summary>
  /// <remarks>Never replaces an existing file/socket and never changes the process umask.</remarks>
  public static UnixAcceptor Bind(LocalEndpoint endpoint) {
    if (OperatingSystem.IsWindows() ||
        (!OperatingSystem.IsLinux() &&
         !OperatingSystem.IsMacOS())) {
      throw new PlatformNotSupportedException();
    }

    ArgumentNullException.ThrowIfNull(endpoint);

    if (endpoint.Kind != LocalEndpointKind.UnixSocket) {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    var name = Path.GetFileName(endpoint.Path);
    if (string.IsNullOrEmpty(name) ||
        name is "." or "..") {
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }

    var suppliedParent = Path.GetDirectoryName(endpoint.Path);
    if (string.IsNullOrEmpty(suppliedParent)) {
      suppliedParent = ".";
    }

    var uid = UnixNative.EffectiveUid;
    var originalParent = RequirePrivateParent(suppliedParent, uid);
    var canonicalParent = UnixNative.Canonicalize(suppliedParent);
    var parent = RequirePrivateParent(canonicalParent, uid);
    if (!parent.SameFile(originalParent)) {
      throw new TransportException(TransportErrorKind.AccessPolicy);
    }

    var path = Path.Combine(canonicalParent, name);
    if (UnixNative.TryInspectNoFollow(path, out var _)) {
      throw new TransportException(TransportErrorKind.Io, new IOException("Socket path already exists."));
    }

    var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    UnixAcceptor? bound = null;
    try {
      // Managed Socket.Bind tracks and unlinks the filesystem path on Dispose.
      // Native bind leaves path removal exclusively to identity-checked Cleanup.
      UnixNative.BindSocket(listener, path);
      listener.Listen();
      listener.Blocking = false;
      var socket = UnixNative.InspectNoFollow(path);
      if (!socket.IsSocket ||
          socket.Uid != uid) {
        throw new TransportException(TransportErrorKind.AccessPolicy);
      }

      bound = new UnixAcceptor(listener, path, uid, parent, socket);
      File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
      if (!RequirePrivateParent(canonicalParent, uid).SameFile(parent)) {
        throw new TransportException(TransportErrorKind.AccessPolicy);
      }

      return bound;
    }
    catch (SocketException error) {
      if (bound is not null) {
        TryCleanup(bound);
      }

      listener.Dispose();
      throw new TransportException(TransportErrorKind.Io, error);
    }
    catch (IOException error) {
      if (bound is not null) {
        TryCleanup(bound);
      }

      listener.Dispose();
      throw new TransportException(TransportErrorKind.Io, error);
    }
    catch (ArgumentException) {
      if (bound is not null) {
        TryCleanup(bound);
      }

      listener.Dispose();
      throw new TransportException(TransportErrorKind.InvalidEndpoint);
    }
    catch (UnauthorizedAccessException error) {
      if (bound is not null) {
        TryCleanup(bound);
      }

      listener.Dispose();
      throw new TransportException(TransportErrorKind.AccessPolicy, error);
    }
    catch {
      if (bound is not null) {
        TryCleanup(bound);
      }

      listener.Dispose();
      // A path not yet verified as our original socket is preserved for investigation.
      throw;
    }
  }

  /// <summary>Removes only the socket recorded at bind, after rechecking its parent and inode.</summary>
  /// <remarks>Repeated successful calls do not remove a later same-name file. A detected replacement is preserved.</remarks>
  public void Cleanup() {
    if (_cleaned) {
      return;
    }

    var parent = RequirePrivateParent(Path.GetDirectoryName(Endpoint.Path)!, _uid);
    if (!parent.SameFile(_parentIdentity)) {
      throw new TransportException(TransportErrorKind.AccessPolicy);
    }

    if (UnixNative.TryInspectNoFollow(Endpoint.Path, out var socket)) {
      if (!socket.IsSocket ||
          socket.Uid != _uid ||
          !socket.SameFile(_socketIdentity)) {
        throw new TransportException(TransportErrorKind.AccessPolicy);
      }

      try { File.Delete(Endpoint.Path); }
      catch (IOException error) { throw new TransportException(TransportErrorKind.Io, error); }
      catch (UnauthorizedAccessException error) { throw new TransportException(TransportErrorKind.AccessPolicy, error); }
    }

    Interlocked.Exchange(ref _listener, null)?.Dispose();
    _cleaned = true;
  }

  private static UnixFileIdentity RequirePrivateParent(string path, uint uid) {
    var metadata = UnixNative.InspectNoFollow(path);
    if (!metadata.IsPrivateDirectory(uid)) {
      throw new TransportException(TransportErrorKind.AccessPolicy);
    }

    return metadata;
  }

  private static void TryCleanup(UnixAcceptor bound) {
    try { bound.Cleanup(); }
    catch (TransportException) {
      /* Preserve a changed path for explicit investigation. */
    }
  }
}
