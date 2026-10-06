// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Attributes;

/// <summary>Requests an explicit handler adapter for an implemented static method.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AssuanCommandHandlerAttribute : Attribute {
  /// <summary>Declares the case-sensitive Assuan name and public HELP description.</summary>
  public AssuanCommandHandlerAttribute(string commandName, string description) {
    CommandName = commandName;
    Description = description;
  }

  /// <summary>Command token.</summary>
  public string CommandName { get; }

  /// <summary>Public HELP description.</summary>
  public string Description { get; }
}
