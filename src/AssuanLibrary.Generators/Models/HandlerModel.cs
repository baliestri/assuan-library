// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AssuanLibrary.Generators.Models;

internal sealed class HandlerModel : IEquatable<HandlerModel> {
  private HandlerModel(string ns, string type, string access, bool staticType, string method,
  string state, string name, string description, string? errorId, SourceLocation location) {
    Namespace = ns;
    TypeName = type;
    Accessibility = access;
    StaticType = staticType;
    Method = method;
    Property = method + "Handler";
    StateType = state;
    Name = name;
    Description = description;
    ErrorId = errorId;
    Location = location;
  }

  internal string Namespace { get; }
  internal string TypeName { get; }
  internal string Accessibility { get; }
  internal bool StaticType { get; }
  internal string Method { get; }
  internal string Property { get; }
  internal string StateType { get; }
  internal string Name { get; }
  internal string Description { get; }
  internal string? ErrorId { get; }
  internal SourceLocation Location { get; }

  public bool Equals(HandlerModel? other) => other is not null && Namespace == other.Namespace &&
                                             TypeName == other.TypeName && Accessibility == other.Accessibility && StaticType == other.StaticType &&
                                             Method == other.Method && StateType == other.StateType && Name == other.Name &&
                                             Description == other.Description && ErrorId == other.ErrorId &&
                                             (ErrorId is null || (Location.Start == other.Location.Start &&
                                                                  ReferenceEquals(Location.ToLocation().SourceTree,
                                                                    other.Location.ToLocation().SourceTree)));

  internal static HandlerModel Extract(GeneratorAttributeSyntaxContext context,
  CancellationToken token)
    => ExtractFromSymbol((IMethodSymbol)context.TargetSymbol, context.Attributes[0], token);

  internal static HandlerModel ExtractFromSymbol(IMethodSymbol method, AttributeData attr,
  CancellationToken token) {
    var type = method.ContainingType;
    var location = new SourceLocation(attr.ApplicationSyntaxReference?.GetSyntax(token).GetLocation() ??
                                      method.Locations[0]);
    var supported = type.ContainingType is null && type is {
      Arity: 0, DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Public or Microsoft.CodeAnalysis.Accessibility.Internal
    } &&
                    type.DeclaringSyntaxReferences.All(reference => reference.GetSyntax(token) is ClassDeclarationSyntax declaration &&
                                                                    declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    var error = !supported ? "ASG001" : null;
    var property = method.Name + "Handler";
    var signature = method is { IsStatic: true, IsGenericMethod: false, MethodKind: MethodKind.Ordinary } &&
                    method.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(token) is MethodDeclarationSyntax declaration &&
                                                                      (declaration.Body is not null || declaration.ExpressionBody is not null)) &&
                    method.Parameters.Length == 2 &&
                    method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                    "global::System.Threading.Tasks.ValueTask" &&
                    method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                    "global::System.Threading.CancellationToken" &&
                    method.Parameters[0].Type is INamedTypeSymbol parameter &&
                    parameter.OriginalDefinition.MetadataName == "CommandContext`1" &&
                    parameter.ContainingNamespace.ToDisplayString() == "AssuanLibrary.Server";
    if (error is null &&
        (!signature || type.GetMembers(method.Name).OfType<IMethodSymbol>().Count() != 1)) {
      error = "ASG006";
    }

    if (error is null &&
        type.GetMembers(property).Length != 0) {
      error = "ASG002";
    }

    if (error is null &&
        signature &&
        !VisibleState(((INamedTypeSymbol)method.Parameters[0].Type).TypeArguments[0], type.DeclaredAccessibility)) {
      error = "ASG006";
    }

    var args = attr.ConstructorArguments;
    var name = args.Length > 0 ? args[0].Value as string ?? string.Empty : string.Empty;
    var description = args.Length > 1 ? args[1].Value as string ?? string.Empty : string.Empty;
    if (error is null &&
        (!ValidName(name) || description.IndexOfAny(['\0', '\r', '\n']) >= 0)) {
      error = "ASG007";
    }

    var state = signature
      ? ((INamedTypeSymbol)method.Parameters[0].Type).TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
      : "global::System.Object";
    var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
    var access = type.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public ? "public" : "internal";
    var methodIdentifier = SyntaxFacts.GetKeywordKind(method.Name) == SyntaxKind.None ? method.Name : "@" + method.Name;
    return new HandlerModel(ns, type.Name, access, type.IsStatic, methodIdentifier,
      state, name, description, error, error is null ? default : location);
  }

  private static bool ValidName(string name) {
    if (name.Length is 0 or >= 1000 ||
        name[0] == '#' ||
        name is "NOP" or "BYE" or "HELP" or "RESET" or "OPTION") {
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

  internal static bool VisibleState(ITypeSymbol state, Accessibility owner) {
    if (state is IArrayTypeSymbol array) {
      return VisibleState(array.ElementType, owner);
    }

    if (state is not INamedTypeSymbol named) {
      return true;
    }

    for (var part = named; part is not null; part = part.ContainingType) {
      if (part.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private ||
          (owner == Microsoft.CodeAnalysis.Accessibility.Public &&
           part.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public)) {
        return false;
      }
    }

    return named.TypeArguments.All(argument => VisibleState(argument, owner));
  }

  public override bool Equals(object? obj)
    => obj is HandlerModel other && Equals(other);

  public override int GetHashCode() {
    unchecked {
      var hash = (Namespace.GetHashCode() * 31) + TypeName.GetHashCode();
      return (hash * 31) + (Method.GetHashCode() * 31) + Name.GetHashCode();
    }
  }
}
