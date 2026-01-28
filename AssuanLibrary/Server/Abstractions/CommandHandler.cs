// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol.Abstractions;

namespace AssuanLibrary.Server.Abstractions;

/// <summary>
///   Represents a handler for Assuan commands.
/// </summary>
public abstract class CommandHandler {
  /// <summary>
  ///   Gets the name of the Assuan command this handler processes.
  /// </summary>
  public abstract string Name { get; }

  /// <summary>
  ///   Handles the specified Assuan command in the given server context.
  /// </summary>
  /// <param name="command">The Assuan command to handle.</param>
  /// <param name="serverContext">The server context.</param>
  /// <exception cref="NotImplementedException">Thrown if neither Handle nor HandleAsync is implemented.</exception>
  public virtual void Handle(IReadOnlyAssuanCommand command, IServerContext serverContext)
    => throw new NotImplementedException("Either Handle or HandleAsync must be implemented.");

  /// <summary>
  ///   Handles the specified Assuan command in the given server context asynchronously.
  /// </summary>
  /// <param name="command">The Assuan command to handle.</param>
  /// <param name="serverContext">The server context.</param>
  /// <returns>A task that represents the asynchronous handle operation.</returns>
  /// <remarks>
  ///   The <see cref="CancellationToken" /> to observe is included in the <see cref="IServerContext.Session" />.
  /// </remarks>
  public virtual Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext)
    => Task.Run(() => Handle(command, serverContext));
}
