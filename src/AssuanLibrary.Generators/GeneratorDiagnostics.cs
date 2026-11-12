// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using Microsoft.CodeAnalysis;

namespace AssuanLibrary.Generators;

internal static class GeneratorDiagnostics {
  internal readonly static DiagnosticDescriptor UnsupportedType = Create("ASG001", "Unsupported annotated class");

  internal readonly static DiagnosticDescriptor InvalidMember =
    Create("ASG002", "Invalid or conflicting generated member");

  internal readonly static DiagnosticDescriptor InvalidHandler = Create("ASG006", "Invalid command handler method");
  internal readonly static DiagnosticDescriptor InvalidHandlerMetadata = Create("ASG007", "Invalid handler metadata");

  internal readonly static DiagnosticDescriptor InvalidGroup =
    Create("ASG008", "Invalid command group or inconsistent state");

  internal readonly static DiagnosticDescriptor InvalidOptionSignature =
    Create("ASG009", "Invalid option handler signature");

  internal readonly static DiagnosticDescriptor InvalidOptionMetadata =
    Create("ASG010", "Invalid or duplicate option name or missing group");

  internal readonly static DiagnosticDescriptor IntegrationCollision =
    Create("ASG011", "Generated integration type conflicts with consumer type");

  private static DiagnosticDescriptor Create(string id, string title)
    => new(id, title, "{0} at byte {1}", "AssuanLibrary", DiagnosticSeverity.Error, true);
}
