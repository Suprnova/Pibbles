namespace Pibbles.Diagnostics;

/// <summary>Every diagnostic code Pibbles reports.</summary>
public static class DiagnosticCatalog
{
    /// <summary>Every registered descriptor.</summary>
    public static IReadOnlyList<DiagnosticDescriptor> All { get; } = [];
}
