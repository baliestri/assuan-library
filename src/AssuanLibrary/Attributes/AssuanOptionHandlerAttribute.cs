// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Attributes;

/// <summary>Marks an implemented static method as an OPTION route within an Assuan handler group.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AssuanOptionHandlerAttribute : Attribute {
  /// <summary>Declares a literal ASCII option name without the optional wire prefix --.</summary>
  public AssuanOptionHandlerAttribute(string optionName) => OptionName = optionName;

  /// <summary>Case-sensitive option name passed by OptionRequest.</summary>
  public string OptionName { get; }
}
