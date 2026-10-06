// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AssuanLibrary.Generators.Models;

internal sealed class GroupModel : IEquatable<GroupModel> {
  private GroupModel(string ns, string type, string access, bool staticType, string state,
  ImmutableArray<string> handlers, ImmutableArray<OptionEntry> options, string? errorId, SourceLocation location) {
    Namespace = ns;
    TypeName = type;
    Accessibility = access;
    StaticType = staticType;
    StateType = state;
    HandlerProperties = handlers;
    Options = options;
    ErrorId = errorId;
    Location = location;
  }

  internal string Namespace { get; }
  internal string TypeName { get; }
  internal string Accessibility { get; }
  internal bool StaticType { get; }
  internal string StateType { get; }
  internal ImmutableArray<string> HandlerProperties { get; }
  internal ImmutableArray<OptionEntry> Options { get; }
  internal string? ErrorId { get; }
  internal SourceLocation Location { get; }

  public bool Equals(GroupModel? other)
    => other is not null && Namespace == other.Namespace &&
       TypeName == other.TypeName && Accessibility == other.Accessibility && StaticType == other.StaticType &&
       StateType == other.StateType && ErrorId == other.ErrorId &&
       HandlerProperties.SequenceEqual(other.HandlerProperties) && Options.SequenceEqual(other.Options) &&
       (ErrorId is null || (Location.Start == other.Location.Start &&
                            ReferenceEquals(Location.ToLocation().SourceTree, other.Location.ToLocation().SourceTree)));

  internal static GroupModel Extract(GeneratorAttributeSyntaxContext context,
  CancellationToken token) {
    var type = (INamedTypeSymbol)context.TargetSymbol;
    var attr = context.Attributes[0];
    var location = new SourceLocation(attr.ApplicationSyntaxReference?.GetSyntax(token).GetLocation() ??
                                      context.TargetNode.GetLocation());
    var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
    var access = type.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public ? "public" : "internal";
    var supported = type.ContainingType is null && type is {
                      Arity: 0, DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Public or Microsoft.CodeAnalysis.Accessibility.Internal
                    } &&
                    type.DeclaringSyntaxReferences.All(reference => reference.GetSyntax(token) is ClassDeclarationSyntax declaration &&
                                                                    declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    var methods = type.GetMembers().OfType<IMethodSymbol>().Select(method => (Method: method,
        Attribute: method.GetAttributes().FirstOrDefault(value => value.AttributeClass?.ToDisplayString() ==
                                                                  "AssuanLibrary.Attributes.AssuanCommandHandlerAttribute")))
      .Where(item => item.Attribute is not null)
      .Select(item => HandlerModel.ExtractFromSymbol(item.Method, item.Attribute!, token)).ToArray();
    var optionModels = type.GetMembers().OfType<IMethodSymbol>().Select(method => (Method: method,
        Attribute: method.GetAttributes().FirstOrDefault(value => value.AttributeClass?.ToDisplayString() ==
                                                                  "AssuanLibrary.Attributes.AssuanOptionHandlerAttribute")))
      .Where(item => item.Attribute is not null)
      .Select(item => OptionModel.ExtractFromSymbol(item.Method, item.Attribute!, token)).ToArray();
    var states = methods.Where(item => item.ErrorId is null).Select(item => item.StateType)
      .Concat(optionModels.Where(item => item.ErrorId is null).Select(item => item.StateType))
      .Distinct(StringComparer.Ordinal).ToArray();
    var error = !supported ? "ASG001" : null;
    if (error is null &&
        type.GetMembers("RegisterHandlers").Length != 0) {
      error = "ASG002";
    }

    if (error is null &&
        optionModels.Length != 0 &&
        type.GetMembers("CreateHooks").Length != 0) {
      error = "ASG002";
    }

    if (error is null &&
        optionModels.Length != 0 &&
        (type.GetMembers("__AssuanOptionHooks").Length != 0 ||
         Enumerable.Range(0, optionModels.Length).Any(index =>
           type.GetMembers("__assuan_option_" + index).Length != 0))) {
      error = "ASG002";
    }

    if (error is null &&
        (methods.Length + optionModels.Length == 0 || methods.Any(item => item.ErrorId is not null) ||
         optionModels.Any(item => item.ErrorId is not null) || states.Length != 1 ||
         methods.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != methods.Length ||
         optionModels.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != optionModels.Length)) {
      error = "ASG008";
    }

    var properties = methods.OrderBy(item => item.Method, StringComparer.Ordinal)
      .Select(item => item.Property).ToImmutableArray();
    var options = optionModels.OrderBy(item => item.Name, StringComparer.Ordinal)
      .Select(item => new OptionEntry(item.Method, item.Name)).ToImmutableArray();

    return new GroupModel(ns, type.Name, access, type.IsStatic,
      states.Length == 1 ? states[0] : "global::System.Object", properties, options,
      error, error is null ? default : location);
  }

  public override bool Equals(object? obj)
    => obj is GroupModel other && Equals(other);

  public override int GetHashCode() {
    unchecked {
      var hash = (Namespace.GetHashCode() * 31) + TypeName.GetHashCode();
      hash = (hash * 31) + StateType.GetHashCode();

      foreach (var property in HandlerProperties) {
        hash = (hash * 31) + property.GetHashCode();
      }

      foreach (var option in Options) {
        hash = (hash * 31) + option.GetHashCode();
      }

      return hash;
    }
  }

  internal readonly struct OptionEntry : IEquatable<OptionEntry> {
    internal OptionEntry(string method, string name) {
      Method = method;
      Name = name;
    }

    internal string Method { get; }
    internal string Name { get; }

    public bool Equals(OptionEntry other)
      => Method == other.Method && Name == other.Name;

    public override bool Equals(object? obj)
      => obj is OptionEntry other && Equals(other);

    public override int GetHashCode()
      => (Method.GetHashCode() * 31) + Name.GetHashCode();
  }
}
