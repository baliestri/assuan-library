// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using AssuanLibrary.Generators.Generation;
using AssuanLibrary.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AssuanLibrary.Generators;

/// <summary>Incremental command adapter, registry and OPTION dispatcher generator.</summary>
[Generator]
public sealed class AssuanIncrementalGenerator : IIncrementalGenerator {
  /// <inheritdoc />
  public void Initialize(IncrementalGeneratorInitializationContext context) {
    var handlers = context.SyntaxProvider.ForAttributeWithMetadataName(
      "AssuanLibrary.Attributes.AssuanCommandHandlerAttribute",
      static (node, _) => node is MethodDeclarationSyntax,
      static (syntax, token) => HandlerModel.Extract(syntax, token));
    context.RegisterSourceOutput(handlers.WithComparer(EqualityComparer<HandlerModel>.Default),
      EmitHandler);
    var groups = context.SyntaxProvider.ForAttributeWithMetadataName(
      "AssuanLibrary.Attributes.AssuanHandlerGroupAttribute",
      static (node, _) => node is ClassDeclarationSyntax,
      static (syntax, token) => GroupModel.Extract(syntax, token));
    context.RegisterSourceOutput(groups.WithComparer(EqualityComparer<GroupModel>.Default),
      EmitGroup);
    var options = context.SyntaxProvider.ForAttributeWithMetadataName(
      "AssuanLibrary.Attributes.AssuanOptionHandlerAttribute",
      static (node, _) => node is MethodDeclarationSyntax,
      static (syntax, token) => OptionModel.Extract(syntax, token));
    context.RegisterSourceOutput(options.WithComparer(EqualityComparer<OptionModel>.Default),
      ReportOption);
  }

  private static void EmitHandler(SourceProductionContext context, HandlerModel model) {
    if (model.ErrorId is { } id) {
      var descriptor = id switch {
        "ASG001" => GeneratorDiagnostics._unsupportedType,
        "ASG002" => GeneratorDiagnostics._invalidMember,
        "ASG007" => GeneratorDiagnostics._invalidHandlerMetadata,
        var _ => GeneratorDiagnostics._invalidHandler
      };
      context.ReportDiagnostic(Diagnostic.Create(descriptor, model.Location.ToLocation(),
        "invalid declaration", 0));
      return;
    }

    context.AddSource(HandlerEmitter.Hint(model), HandlerEmitter.Emit(model));
  }

  private static void EmitGroup(SourceProductionContext context, GroupModel model) {
    if (model.ErrorId is { } id) {
      var descriptor = id switch {
        "ASG001" => GeneratorDiagnostics._unsupportedType,
        "ASG002" => GeneratorDiagnostics._invalidMember,
        var _ => GeneratorDiagnostics._invalidGroup
      };
      context.ReportDiagnostic(Diagnostic.Create(descriptor, model.Location.ToLocation(),
        "invalid declaration", 0));
      return;
    }

    context.AddSource(GroupEmitter.Hint(model), GroupEmitter.Emit(model));
  }

  private static void ReportOption(SourceProductionContext context, OptionModel model) {
    if (model.ErrorId is not { } id) {
      return;
    }

    context.ReportDiagnostic(Diagnostic.Create(id == "ASG009"
        ? GeneratorDiagnostics._invalidOptionSignature
        : GeneratorDiagnostics._invalidOptionMetadata,
      model.Location.ToLocation(), "invalid declaration", 0));
  }
}
