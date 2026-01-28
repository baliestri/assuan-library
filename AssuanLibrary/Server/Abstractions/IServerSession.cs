// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   Represents a server session for an Assuan connection.
/// </summary>
public interface IServerSession : IDisposable {
  /// <summary>
  ///   Gets the unique identifier of the session.
  /// </summary>
  Guid Id { get; }

  /// <summary>
  ///   Gets the timestamp when the session was created.
  /// </summary>
  DateTimeOffset CreatedAt { get; }

  /// <summary>
  ///   Gets the timestamp of the last activity in the session.
  /// </summary>
  DateTimeOffset LastActivityAt { get; }

  /// <summary>
  ///   Gets a dictionary to store session-specific items (e.g. metadata, state).
  /// </summary>
  IDictionary<string, object?> Items { get; }

  /// <summary>
  ///   Gets the <see cref="CancellationToken" /> associated with the session.
  /// </summary>
  CancellationToken CancellationToken { get; }

  /// <summary>
  ///   Refreshes the last activity timestamp to the current time.
  /// </summary>
  void RefreshLastActivity();
}
