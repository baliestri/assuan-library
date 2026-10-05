// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;

namespace AssuanLibrary.Server;

/// <summary>Case-sensitive handler registry, frozen before use by shared sessions.</summary>
public sealed class CommandRegistry<TState> {
  private readonly Dictionary<string, HandlerEntry> _entries = new(StringComparer.Ordinal);

  internal IEnumerable<HandlerEntry> Entries => _entries.Values;

  internal bool IsFrozen { get; private set; }

  /// <summary>Registers validated metadata without replacing an existing handler on failure.</summary>
  public void Register(ICommandHandler<TState> handler) {
    ArgumentNullException.ThrowIfNull(handler);

    if (IsFrozen) {
      throw new RegistryException(RegistryErrorKind.Frozen);
    }

    var name = handler.Name;
    var description = handler.Description;
    ValidateName(name);
    if (name is "NOP" or "BYE" or "HELP" or "RESET" or "OPTION") {
      throw new RegistryException(RegistryErrorKind.Reserved);
    }

    if (description is null ||
        description.IndexOfAny('\0', '\r', '\n') >= 0) {
      throw new RegistryException(RegistryErrorKind.InvalidDescription);
    }

    if (_entries.ContainsKey(name)) {
      throw new RegistryException(RegistryErrorKind.Duplicate);
    }

    _entries.Add(name, new HandlerEntry(handler, name, description));
  }

  /// <summary>Seals the registry for read-only sharing across sessions.</summary>
  public void Freeze()
    => IsFrozen = true;

  /// <summary>Finds a handler by its exact case-sensitive token, without allocating.</summary>
  public ICommandHandler<TState>? Find(string name) {
    ArgumentNullException.ThrowIfNull(name);
    return _entries.TryGetValue(name, out var entry) ? entry.Handler : null;
  }

  private static void ValidateName(string? name) {
    if (string.IsNullOrEmpty(name) ||
        name.Length >= 1000 ||
        name.Any(value => value is < (char)33 or > (char)126)) {
      throw new RegistryException(RegistryErrorKind.InvalidName);
    }

    try { _ = ProtocolCommand.Create(Encoding.ASCII.GetBytes(name), ReadOnlySpan<byte>.Empty); }
    catch (ProtocolException) { throw new RegistryException(RegistryErrorKind.InvalidName); }
  }

  internal readonly record struct HandlerEntry(ICommandHandler<TState> Handler, string Name, string Description);
}
