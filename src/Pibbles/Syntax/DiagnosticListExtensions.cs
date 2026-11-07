using Pibbles.Diagnostics;

namespace Pibbles.Syntax;

internal static class DiagnosticListExtensions
{
    /// <summary>Adds a diagnostic for <paramref name="span"/> of <paramref name="source"/>.</summary>
    public static void Report(this List<Diagnostic> diagnostics, SourceText source, DiagnosticDescriptor descriptor, TextSpan span, params object?[] arguments) =>
        diagnostics.Add(descriptor.Create(source.GetLocation(span), arguments));
}
