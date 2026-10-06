// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace AssuanLibrary.Attributes;

/// <summary>Requests explicit typed registration of annotated methods in a partial class.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AssuanHandlerGroupAttribute : Attribute { }
