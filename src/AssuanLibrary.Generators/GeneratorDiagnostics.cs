// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using Microsoft.CodeAnalysis;

namespace AssuanLibrary.Generators;

internal static class GeneratorDiagnostics {
  internal static readonly DiagnosticDescriptor _unsupportedType = Create("ASG001", "Unsupported annotated class");
  internal static readonly DiagnosticDescriptor _invalidMember = Create("ASG002", "Invalid or conflicting generated member");
  internal static readonly DiagnosticDescriptor _invalidHandler = Create("ASG006", "Invalid command handler method");
  internal static readonly DiagnosticDescriptor _invalidHandlerMetadata = Create("ASG007", "Invalid handler metadata");
  internal static readonly DiagnosticDescriptor _invalidGroup = Create("ASG008", "Invalid command group or inconsistent state");
  internal static readonly DiagnosticDescriptor _invalidOptionSignature = Create("ASG009", "Invalid option handler signature");
  internal static readonly DiagnosticDescriptor _invalidOptionMetadata = Create("ASG010", "Invalid or duplicate option name or missing group");
  internal static readonly DiagnosticDescriptor _integrationCollision = Create("ASG011", "Generated integration type conflicts with consumer type");

  private static DiagnosticDescriptor Create(string id, string title)
    => new(id, title, "{0} at byte {1}", "AssuanLibrary", DiagnosticSeverity.Error, true);
}
