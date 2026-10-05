# Local IPC transports

`AssuanLibrary.Transport` exposes local byte-stream endpoints alongside TCP: Unix domain sockets on Linux/macOS and named pipes on Windows. Both produce owned `Stream` objects that can be passed to `AssuanChannel`. Listener access policy is **current user only**; failure to establish or verify it prevents a listener from admitting connections. Connecting to an existing service does not require that service to follow this library's listener-directory policy.

## Unix domain sockets

`UnixAcceptor.Bind` requires an existing immediate parent directory owned by the process's effective UID and inaccessible to group/other users (typically mode `0700`). It rejects a symlinked immediate parent, an existing endpoint of any type and identity changes while resolving the parent. The resulting filesystem socket is restricted to mode `0600` without changing the process umask. An accepted peer is delivered only after the OS reports the same effective UID; `UnixPeerIdentity` contains UID, GID and PID when supplied by the platform. TCP addresses have no such identity.

```csharp
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Unix;

if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
// Run from a private working directory inside your chosen workspace.
string parent = Path.Combine(Environment.CurrentDirectory, "artifacts", "ipc-demo");
Directory.CreateDirectory(parent);
File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
var endpoint = LocalEndpoint.Unix(Path.Combine(parent, "socket"));
await using var listener = UnixAcceptor.Bind(endpoint);
try {
  var accepting = listener.AcceptAsync(CancellationToken.None);
  using var client = await UnixConnector.ConnectAsync(listener.Endpoint);
  var connection = await accepting;
  using var peer = connection.Stream;
  Console.WriteLine(connection.PeerIdentity is UnixPeerIdentity); // true after OS verification
} finally {
  listener.Cleanup(); // Removes only the socket that this listener created.
}
```

`Cleanup()` rechecks parent ownership, mode, device/inode and the socket's owner/type/device/inode before unlinking. A changed parent, symlink or replaced socket is preserved and causes `AccessPolicy`, never a blind deletion by matching path. Repeating successful cleanup cannot remove a later file with the same name. Disposing a listener **closes its socket but does not unlink its pathname**; call `Cleanup()` explicitly when appropriate. Same-UID and privileged processes remain in the local trust boundary and can race pathname checks.

The Unix implementation uses small OS-specific native metadata and peer-credential calls for Linux/macOS on little-endian x64/arm64. Unsupported native layouts fail closed. Its permission, UID, and cleanup tests pass in a Linux .NET 10 container with a private tmpfs socket directory. **macOS remains unverified** and needs its own platform run; a Windows build is not evidence of Unix interoperability. Outgoing connections can target external services in directories with different policies. Binary non-UTF-8 pathnames returned by `gpgconf` belong to the later agent-discovery stage.

## Windows named pipes

Use a local `\\.\pipe\<name>` path: empty names, remote server paths, nested names, NUL and paths over 256 UTF-16 code units are rejected. `PipeAcceptor.Bind` installs an explicit protected DACL allowing only the current process user's SID, rejects remote clients, uses noninheritable byte-mode handles and atomically refuses a name already bound by another listener. A waiting instance is maintained so one listener can serve successive clients.

```csharp
using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Windows;

var endpoint = LocalEndpoint.NamedPipe(@"\\.\pipe\assuan-example");
await using var listener = PipeAcceptor.Bind(endpoint);
var accepting = listener.AcceptAsync(CancellationToken.None);
using var client = await PipeConnector.ConnectAsync(endpoint);
var connection = await accepting;
using var server = connection.Stream;
Console.WriteLine(connection.PeerIdentity is null); // ACL is access control, not queried peer identity.
```

When a pipe is busy, `PipeConnector.ConnectAsync` retries with a short asynchronous delay under a **single total connection deadline** (ten seconds by default); cancellation does not leave a blocking worker to connect later. A pipe's `FlushAsync` does not wait for the other end to read bytes already written to the kernel. Closing drops the pipe directly, so unread bytes may be lost; higher-level sessions must receive required acknowledgements before closing. Named pipes do not promise TCP half-close semantics. Windows DACL protection does **not** populate `PeerIdentity` and does not replace application authorization.

## Ownership and errors

The receiver owns each accepted/connected stream. `TransportException.Kind` distinguishes endpoint/options, policy, timeout/cancellation, closed transport and I/O failures; the exception's own message does not contain a socket pathname or arbitrary OS error text. `IoCause` is available for explicitly reviewed diagnostics. Authentication beyond the local access policy, secret-buffer cleanup and session deadlines remain the application's and higher layers' responsibilities. No additional NuGet package is required for the Windows ACL implementation under the library's `net10.0` target.
