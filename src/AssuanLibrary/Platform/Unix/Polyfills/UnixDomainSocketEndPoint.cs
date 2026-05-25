// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;

namespace AssuanLibrary.Platform.Unix.Polyfills;

/// <summary>
///   Represents a Unix Domain Socket endpoint.
/// </summary>
/// <param name="path">The path to the Unix Domain Socket file.</param>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class UnixDomainSocketEndPoint(string path) : EndPoint {
  private string _path = !string.IsNullOrWhiteSpace(path)
    ? path
    : throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

  /// <inheritdoc />
  public override AddressFamily AddressFamily => AddressFamily.Unix;

  /// <inheritdoc />
  public override EndPoint Create(SocketAddress socketAddress) {
    var addr = (int)AddressFamily.Unix;

    if (socketAddress[0] != (addr & 0xFF) ||
        socketAddress[1] != (addr & 0xFF) >> 8) {
      throw new ArgumentException("The SocketAddress is not a Unix SocketAddress.", nameof(socketAddress));
    }

    if (socketAddress.Size == 2) {
      var uep = new UnixDomainSocketEndPoint("a");
      _path = string.Empty;

      return uep;
    }

    var size = socketAddress.Size - 2;
    var nameBytes = new byte[size];

    for (var i = 0; i < nameBytes.Length; i++) {
      nameBytes[i] = socketAddress[i + 2];

      if (nameBytes[i] != 0) {
        continue;
      }

      size = i;
      break;
    }

    var name = Encoding.UTF8.GetString(nameBytes, 0, size);
    return new UnixDomainSocketEndPoint(name);
  }

  /// <inheritdoc />
  public override SocketAddress Serialize() {
    var nameBytes = Encoding.UTF8.GetBytes(_path);
    var socketAddress = new SocketAddress(AddressFamily, nameBytes.Length + 3);

    for (var i = 0; i < nameBytes.Length; i++) {
      socketAddress[i + 2] = nameBytes[i];
    }

    socketAddress[nameBytes.Length + 2] = 0;

    return socketAddress;
  }

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is UnixDomainSocketEndPoint other &&
       _path.Equals(other._path, StringComparison.Ordinal);

  /// <inheritdoc />
  public override int GetHashCode()
    => GetEqualityComponents()
      .Select(obj => obj.GetHashCode())
      .Aggregate(17, (current, hash) => (current * 31) + hash);

  /// <inheritdoc />
  public override string ToString()
    => _path;

  private IEnumerable<object> GetEqualityComponents() {
    yield return _path;
  }
}
