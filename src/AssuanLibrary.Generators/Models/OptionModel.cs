// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AssuanLibrary.Generators.Models;

internal sealed class OptionModel : IEquatable<OptionModel> {
  private OptionModel(string method, string name, string state, string? errorId, SourceLocation location) {
    Method = method;
    Name = name;
    StateType = state;
    ErrorId = errorId;
    Location = location;
  }

  internal string Method { get; }
  internal string Name { get; }
  internal string StateType { get; }
  internal string? ErrorId { get; }
  internal SourceLocation Location { get; }

  public bool Equals(OptionModel? other)
    => other is not null && Method == other.Method &&
       Name == other.Name && StateType == other.StateType && ErrorId == other.ErrorId &&
       (ErrorId is null || (Location.Start == other.Location.Start &&
                            ReferenceEquals(Location.ToLocation().SourceTree, other.Location.ToLocation().SourceTree)));

  internal static OptionModel Extract(GeneratorAttributeSyntaxContext context,
  CancellationToken token)
    => ExtractFromSymbol((IMethodSymbol)context.TargetSymbol, context.Attributes[0], token);

  internal static OptionModel ExtractFromSymbol(IMethodSymbol method, AttributeData attr,
  CancellationToken token) {
    var type = method.ContainingType;
    var location = new SourceLocation(attr.ApplicationSyntaxReference?.GetSyntax(token).GetLocation() ??
                                      method.Locations[0]);
    var grouped = type.GetAttributes().Any(item => item.AttributeClass?.ToDisplayString() ==
                                                   "AssuanLibrary.Attributes.AssuanHandlerGroupAttribute");
    var name = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string ?? "" : "";
    var error = grouped ? null : "ASG010";
    var signature = method is { IsStatic: true, IsGenericMethod: false, MethodKind: MethodKind.Ordinary } &&
                    method.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(token) is MethodDeclarationSyntax declaration &&
                                                                      (declaration.Body is not null || declaration.ExpressionBody is not null)) &&
                    method.Parameters.Length == 3 &&
                    method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                    "global::System.Threading.Tasks.ValueTask" &&
                    method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                    "global::AssuanLibrary.Server.OptionRequest" &&
                    method.Parameters[2].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                    "global::System.Threading.CancellationToken" &&
                    method.Parameters[1].Type is INamedTypeSymbol parameter &&
                    parameter.OriginalDefinition.MetadataName == "CommandContext`1" &&
                    parameter.ContainingNamespace.ToDisplayString() == "AssuanLibrary.Server";
    if (error is null &&
        (!signature || type.GetMembers(method.Name).OfType<IMethodSymbol>().Count() != 1)) {
      error = "ASG009";
    }

    if (error is null &&
        (!ValidName(name) || type.GetMembers().OfType<IMethodSymbol>().Count(other =>
          other.GetAttributes().Any(item => item.AttributeClass?.ToDisplayString() ==
                                            "AssuanLibrary.Attributes.AssuanOptionHandlerAttribute" &&
                                            item.ConstructorArguments.Length > 0 && item.ConstructorArguments[0].Value as string == name)) != 1)) {
      error = "ASG010";
    }

    if (error is null &&
        signature &&
        !HandlerModel.VisibleState(
          ((INamedTypeSymbol)method.Parameters[1].Type).TypeArguments[0], type.DeclaredAccessibility)) {
      error = "ASG009";
    }

    var state = signature
      ? ((INamedTypeSymbol)method.Parameters[1].Type).TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
      : "global::System.Object";
    var identifier = SyntaxFacts.GetKeywordKind(method.Name) == SyntaxKind.None ? method.Name : "@" + method.Name;
    return new OptionModel(identifier, name, state, error, error is null ? default : location);
  }

  private static bool ValidName(string name) {
    if (name.Length is 0 or >= 1000 ||
        name[0] == '#' ||
        name.StartsWith("--", StringComparison.Ordinal)) {
      return false;
    }

    foreach (var ch in name) {
      if (ch < 33 ||
          ch > 126 ||
          ch == '%') {
        return false;
      }
    }

    return true;
  }

  public override bool Equals(object? obj)
    => obj is OptionModel other && Equals(other);

  public override int GetHashCode() {
    unchecked {
      return (((Method.GetHashCode() * 31) + Name.GetHashCode()) * 31) + StateType.GetHashCode();
    }
  }
}
